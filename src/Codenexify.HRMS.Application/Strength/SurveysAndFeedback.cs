using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

// ====================================================================== surveys

public sealed class SurveyQuestionInput
{
    [StringLength(300)] public string? Text { get; set; }
    public QuestionType Type { get; set; } = QuestionType.Rating;
    [StringLength(1000)] public string? Options { get; set; }
    public bool Required { get; set; } = true;
}

public sealed class SurveyInput
{
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public SurveyKind Kind { get; set; } = SurveyKind.Pulse;
    [Display(Name = "Anonymous")] public bool IsAnonymous { get; set; } = true;
    [Display(Name = "Department (blank = everyone)")] public int? DepartmentId { get; set; }
    [Range(1, 60), Display(Name = "Open for (days)")] public int Days { get; set; } = 7;
    [Range(3, 50), Display(Name = "Minimum responses before results show")] public int MinResponses { get; set; } = 3;
    public List<SurveyQuestionInput> Questions { get; set; } = [];
}

public sealed record SurveyRow(int Id, string Title, SurveyKind Kind, bool IsAnonymous, SurveyStatus Status, string? Department, DateTime? OpenedAtUtc,
    DateTime? ClosesAtUtc, int ResponseCount, int QuestionCount);
public sealed record SurveyToTake(int Id, string Title, string? Description, SurveyKind Kind, bool IsAnonymous, DateTime? ClosesAtUtc, IReadOnlyList<SurveyQuestion> Questions);
public sealed record QuestionResult(int QuestionId, string Text, QuestionType Type, int Answered, decimal? Average, int? Enps,
    IReadOnlyList<(string Label, int Count)> Distribution, IReadOnlyList<string> Comments);
public sealed record QuestionOutline(string Text, QuestionType Type, IReadOnlyList<string> Options, bool Required);
public sealed record SurveyResults(SurveyRow Survey, bool Visible, int MinResponses, IReadOnlyList<QuestionResult> Questions, int EligibleCount, IReadOnlyList<QuestionOutline> Outline);

public interface ISurveyService
{
    Task<IReadOnlyList<SurveyRow>> ListAsync(CancellationToken ct = default);
    Task<Result<int>> CreateAsync(SurveyInput input, bool exitSurveysEnabled, CancellationToken ct = default);
    Task<Result> PublishAsync(int id, CancellationToken ct = default);
    Task<Result> CloseAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteDraftAsync(int id, CancellationToken ct = default);
    Task<SurveyResults?> ResultsAsync(int id, CancellationToken ct = default);
    /// <summary>Open surveys the signed-in user may answer and hasn't answered yet.</summary>
    Task<IReadOnlyList<SurveyRow>> PendingForMeAsync(CancellationToken ct = default);
    Task<SurveyToTake?> ToTakeAsync(int id, CancellationToken ct = default);
    Task<Result> SubmitAsync(int id, IReadOnlyDictionary<int, string?> answers, CancellationToken ct = default);
}

public sealed class SurveyService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : ISurveyService
{
    private int Me => context.UserId is int id && id > 0 ? id : throw new TenantIsolationException("No signed-in user.");

    private IQueryable<SurveyRow> Rows(IQueryable<Survey> q) =>
        q.Select(s => new SurveyRow(s.Id, s.Title, s.Kind, s.IsAnonymous, s.Status, db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            s.OpenedAtUtc, s.ClosesAtUtc, db.SurveyResponses.Count(r => r.SurveyId == s.Id), s.Questions.Count));

    public async Task<IReadOnlyList<SurveyRow>> ListAsync(CancellationToken ct = default)
    {
        await AutoCloseAsync(ct);
        return await Rows(db.Surveys.AsNoTracking().OrderByDescending(s => s.Id)).ToListAsync(ct);
    }

    /// <summary>Surveys past their closing time are closed lazily.</summary>
    private async Task AutoCloseAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.Surveys.Where(s => s.Status == SurveyStatus.Open && s.ClosesAtUtc != null && s.ClosesAtUtc <= now).ToListAsync(ct);
        foreach (var s in due) s.Status = SurveyStatus.Closed;
        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }

    public async Task<Result<int>> CreateAsync(SurveyInput i, bool exitSurveysEnabled, CancellationToken ct = default)
    {
        var questions = i.Questions.Where(q => !string.IsNullOrWhiteSpace(q.Text)).ToList();
        if (questions.Count is 0 or > 20) return Result<int>.Fail("Add between 1 and 20 questions.");
        if (i.Kind == SurveyKind.Exit && !exitSurveysEnabled) return Result<int>.Fail("Exit surveys are not included in your plan.");
        if (i.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Department not found.");
        var built = new List<SurveyQuestion>();
        foreach (var (q, n) in questions.Select((q, n) => (q, n + 1)))
        {
            string? options = null;
            if (q.Type == QuestionType.Choice)
            {
                var opts = (q.Options ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
                if (opts.Count is < 2 or > 10) return Result<int>.Fail($"Question {n}: give 2–10 choices, one per line.");
                options = string.Join("\n", opts);
            }
            built.Add(new SurveyQuestion { SortOrder = n, Text = q.Text!.Trim(), Type = q.Type, Options = options, Required = q.Required });
        }
        var survey = new Survey
        {
            Title = i.Title.Trim(), Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), Kind = i.Kind,
            IsAnonymous = i.IsAnonymous, DepartmentId = i.Kind == SurveyKind.Exit ? null : i.DepartmentId, Status = SurveyStatus.Draft,
            MinResponses = i.IsAnonymous ? Math.Max(3, i.MinResponses) : 1, DurationDays = i.Days, Questions = built
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(survey.Id);
    }

    private IQueryable<int> EligibleUsers(Survey s)
    {
        var employees = db.Employees.Where(e => e.UserId != null && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (s.Kind == SurveyKind.Exit)
            employees = employees.Where(e => db.ExitRequests.Any(x => x.EmployeeId == e.Id && (x.Status == ExitStatus.Pending || x.Status == ExitStatus.Approved)));
        else if (s.DepartmentId is int d) employees = employees.Where(e => e.DepartmentId == d);
        return employees.Select(e => e.UserId!.Value);
    }

    public async Task<Result> PublishAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Surveys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Survey not found.");
        if (s.Status != SurveyStatus.Draft) return Result.Fail("Only drafts can be published.");
        s.Status = SurveyStatus.Open;
        s.OpenedAtUtc = clock.UtcNow;
        s.ClosesAtUtc = clock.UtcNow.AddDays(Math.Clamp(s.DurationDays, 1, 60));
        var users = await EligibleUsers(s).ToListAsync(ct);
        notifications.Queue(users.Where(u => u != context.UserId), s.Kind == SurveyKind.Exit ? "Exit survey" : "New survey",
            $"{s.Title}{(s.IsAnonymous ? " (anonymous)" : "")} — open until {s.ClosesAtUtc:dd MMM}.", $"/me/surveys/{s.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CloseAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Surveys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Survey not found.");
        if (s.Status != SurveyStatus.Open) return Result.Fail("The survey is not open.");
        s.Status = SurveyStatus.Closed;
        s.ClosesAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteDraftAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Surveys.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Survey not found.");
        if (s.Status != SurveyStatus.Draft) return Result.Fail("Published surveys can't be deleted; close them instead.");
        db.SurveyQuestions.RemoveRange(s.Questions);
        db.Surveys.Remove(s);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<SurveyResults?> ResultsAsync(int id, CancellationToken ct = default)
    {
        await AutoCloseAsync(ct);
        var row = await Rows(db.Surveys.AsNoTracking().Where(s => s.Id == id)).FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var survey = await db.Surveys.AsNoTracking().Include(s => s.Questions).FirstAsync(s => s.Id == id, ct);
        var eligible = await EligibleUsers(survey).CountAsync(ct);
        // Anonymous surveys never show anything until enough people have answered.
        var visible = row.ResponseCount >= survey.MinResponses;
        var outline = survey.Questions.OrderBy(q => q.SortOrder)
            .Select(q => new QuestionOutline(q.Text, q.Type, (q.Options ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries), q.Required)).ToList();
        if (!visible) return new SurveyResults(row, false, survey.MinResponses, [], eligible, outline);

        var answers = await db.SurveyAnswers.AsNoTracking().Where(a => db.SurveyResponses.Any(r => r.Id == a.ResponseId && r.SurveyId == id))
            .Select(a => new { a.QuestionId, a.Number, a.Text, a.ResponseId }).ToListAsync(ct);
        var names = survey.IsAnonymous ? new Dictionary<int, string?>()
            : await db.SurveyResponses.AsNoTracking().Where(r => r.SurveyId == id).ToDictionaryAsync(r => r.Id, r => r.RespondentName, ct);
        var results = survey.Questions.OrderBy(q => q.SortOrder).Select(q =>
        {
            var qa = answers.Where(a => a.QuestionId == q.Id).ToList();
            var numbers = qa.Where(a => a.Number is not null).Select(a => a.Number!.Value).ToList();
            IReadOnlyList<(string, int)> dist = q.Type switch
            {
                QuestionType.Rating => Enumerable.Range(1, 5).Select(v => (v.ToString(), numbers.Count(n => n == v))).ToList(),
                QuestionType.Enps => Enumerable.Range(0, 11).Select(v => (v.ToString(), numbers.Count(n => n == v))).ToList(),
                QuestionType.Choice => (q.Options ?? "").Split('\n').Select(o => (o, qa.Count(a => a.Text == o))).ToList(),
                _ => []
            };
            int? enps = q.Type == QuestionType.Enps && numbers.Count > 0
                ? (int)Math.Round(100.0 * (numbers.Count(n => n >= 9) - numbers.Count(n => n <= 6)) / numbers.Count) : null;
            // Free-text comments are shuffled-by-text order so their sequence can't be matched to submission order.
            var comments = q.Type == QuestionType.Text
                ? qa.Where(a => !string.IsNullOrWhiteSpace(a.Text)).Select(a => survey.IsAnonymous ? a.Text! : $"{names.GetValueOrDefault(a.ResponseId) ?? "—"}: {a.Text}").Order().ToList()
                : [];
            return new QuestionResult(q.Id, q.Text, q.Type, qa.Count, q.Type is QuestionType.Rating or QuestionType.Enps && numbers.Count > 0 ? Math.Round((decimal)numbers.Average(), 1) : null,
                enps, dist, comments);
        }).ToList();
        return new SurveyResults(row, true, survey.MinResponses, results, eligible, outline);
    }

    public async Task<IReadOnlyList<SurveyRow>> PendingForMeAsync(CancellationToken ct = default)
    {
        await AutoCloseAsync(ct);
        var me = Me;
        var open = await db.Surveys.AsNoTracking().Where(s => s.Status == SurveyStatus.Open && !db.SurveyParticipations.Any(p => p.SurveyId == s.Id && p.UserId == me)).ToListAsync(ct);
        var mine = new List<int>();
        foreach (var s in open) if (await EligibleUsers(s).AnyAsync(u => u == me, ct)) mine.Add(s.Id);
        return await Rows(db.Surveys.AsNoTracking().Where(s => mine.Contains(s.Id)).OrderBy(s => s.ClosesAtUtc)).ToListAsync(ct);
    }

    public async Task<SurveyToTake?> ToTakeAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Surveys.AsNoTracking().Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id && x.Status == SurveyStatus.Open, ct);
        if (s is null || s.ClosesAtUtc <= clock.UtcNow) return null;
        if (!await EligibleUsers(s).AnyAsync(u => u == Me, ct)) return null;
        if (await db.SurveyParticipations.AnyAsync(p => p.SurveyId == id && p.UserId == Me, ct)) return null;
        return new SurveyToTake(s.Id, s.Title, s.Description, s.Kind, s.IsAnonymous, s.ClosesAtUtc, s.Questions.OrderBy(q => q.SortOrder).ToList());
    }

    public async Task<Result> SubmitAsync(int id, IReadOnlyDictionary<int, string?> raw, CancellationToken ct = default)
    {
        var me = Me;
        var survey = await ToTakeAsync(id, ct);
        if (survey is null) return Result.Fail("This survey is closed, not for you, or already answered.");
        var answers = new List<SurveyAnswer>();
        foreach (var q in survey.Questions)
        {
            raw.TryGetValue(q.Id, out var v);
            v = v?.Trim();
            if (string.IsNullOrEmpty(v))
            {
                if (q.Required) return Result.Fail($"Please answer: {q.Text}");
                continue;
            }
            switch (q.Type)
            {
                case QuestionType.Rating when int.TryParse(v, out var r) && r is >= 1 and <= 5: answers.Add(new SurveyAnswer { QuestionId = q.Id, Number = r }); break;
                case QuestionType.Enps when int.TryParse(v, out var e) && e is >= 0 and <= 10: answers.Add(new SurveyAnswer { QuestionId = q.Id, Number = e }); break;
                case QuestionType.Choice when (q.Options ?? "").Split('\n').Contains(v): answers.Add(new SurveyAnswer { QuestionId = q.Id, Text = v }); break;
                case QuestionType.Text: answers.Add(new SurveyAnswer { QuestionId = q.Id, Text = v.Length > 2000 ? v[..2000] : v }); break;
                default: return Result.Fail($"Invalid answer for: {q.Text}");
            }
        }
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var name = survey.IsAnonymous ? null : await db.Users.Where(u => u.Id == me).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        // Participation (who) and response (what) are separate rows with no link for anonymous surveys.
        db.SurveyParticipations.Add(new SurveyParticipation { SurveyId = id, UserId = me });
        db.SurveyResponses.Add(new SurveyResponse { SurveyId = id, RespondentUserId = survey.IsAnonymous ? null : me, RespondentName = name, SubmittedOn = today, Answers = answers });
        // No counter on the survey row: an audited per-response update would reveal the order people answered in.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Fail("You have already answered this survey."); }
        return Result.Ok();
    }
}

// ====================================================================== anonymous feedback

public sealed class FeedbackInput
{
    [Required, StringLength(60)] public string Category { get; set; } = "Workplace";
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Body { get; set; } = string.Empty;
}

public sealed record FeedbackRow(int Id, string Category, string Subject, FeedbackStatus Status, DateOnly CreatedOn, int Messages, bool AwaitingHr);
public sealed record FeedbackView(FeedbackThread Thread, IReadOnlyList<FeedbackMessage> Messages);

public interface IFeedbackService
{
    IReadOnlyList<string> Categories { get; }
    /// <returns>The private access code, shown to the sender once.</returns>
    Task<Result<string>> SendAsync(FeedbackInput input, CancellationToken ct = default);
    Task<FeedbackView?> OpenByCodeAsync(string code, CancellationToken ct = default);
    Task<Result> FollowUpAsync(string code, string body, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackRow>> ListAsync(bool openOnly, CancellationToken ct = default);
    Task<FeedbackView?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> ReplyAsync(int id, string body, bool close, CancellationToken ct = default);
}

public sealed class FeedbackService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IFeedbackService
{
    public IReadOnlyList<string> Categories { get; } = ["Workplace", "Manager", "Policy", "Harassment or misconduct", "Ideas", "Other"];

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant().Replace("-", ""))));

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    public async Task<Result<string>> SendAsync(FeedbackInput i, CancellationToken ct = default)
    {
        if (!Categories.Contains(i.Category)) return Result<string>.Fail("Choose a category.");
        // 16 random characters from an unambiguous alphabet, shown as XXXX-XXXX-XXXX-XXXX.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = string.Join("-", Enumerable.Range(0, 4).Select(_ => new string(Enumerable.Range(0, 4).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray())));
        var today = await TodayAsync(ct);
        db.FeedbackThreads.Add(new FeedbackThread
        {
            Category = i.Category, Subject = i.Subject.Trim(), Status = FeedbackStatus.Open, AccessCodeHash = Hash(code), CreatedOn = today,
            Messages = [new FeedbackMessage { FromHr = false, Body = i.Body.Trim(), SentOn = today }]
        });
        var hr = await notifications.UsersWithPermissionAsync(TenantPermissions.FeedbackManage, ct);
        notifications.Queue(hr, "New anonymous feedback", $"{i.Category}: {i.Subject.Trim()}", "/org/feedback");
        await db.SaveChangesAsync(ct);
        return Result<string>.Ok(code);
    }

    public async Task<FeedbackView?> OpenByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var hash = Hash(code);
        var t = await db.FeedbackThreads.AsNoTracking().Include(x => x.Messages).FirstOrDefaultAsync(x => x.AccessCodeHash == hash, ct);
        return t is null ? null : new FeedbackView(t, t.Messages.OrderBy(m => m.Id).ToList());
    }

    public async Task<Result> FollowUpAsync(string code, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000) return Result.Fail("Write a message (up to 4000 characters).");
        var hash = Hash(code ?? "");
        var t = await db.FeedbackThreads.FirstOrDefaultAsync(x => x.AccessCodeHash == hash, ct);
        if (t is null) return Result.Fail("That code doesn't match any feedback.");
        if (t.Status == FeedbackStatus.Closed) return Result.Fail("This conversation is closed. Send new feedback instead.");
        // The thread page is a POST result, so a browser refresh resends the form: ignore an exact repeat of the last message.
        var last = await db.FeedbackMessages.Where(m => m.ThreadId == t.Id).OrderByDescending(m => m.Id).Select(m => new { m.FromHr, m.Body }).FirstOrDefaultAsync(ct);
        if (last is { FromHr: false } && last.Body == body.Trim()) return Result.Ok();
        db.FeedbackMessages.Add(new FeedbackMessage { ThreadId = t.Id, FromHr = false, Body = body.Trim(), SentOn = await TodayAsync(ct) });
        t.Status = FeedbackStatus.Open;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<FeedbackRow>> ListAsync(bool openOnly, CancellationToken ct = default) =>
        await db.FeedbackThreads.AsNoTracking().Where(t => !openOnly || t.Status != FeedbackStatus.Closed).OrderByDescending(t => t.Id)
            .Select(t => new FeedbackRow(t.Id, t.Category, t.Subject, t.Status, t.CreatedOn, t.Messages.Count,
                !t.Messages.OrderByDescending(m => m.Id).Select(m => m.FromHr).FirstOrDefault())).Take(200).ToListAsync(ct);

    public async Task<FeedbackView?> GetAsync(int id, CancellationToken ct = default)
    {
        var t = await db.FeedbackThreads.AsNoTracking().Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : new FeedbackView(t, t.Messages.OrderBy(m => m.Id).ToList());
    }

    public async Task<Result> ReplyAsync(int id, string body, bool close, CancellationToken ct = default)
    {
        var t = await db.FeedbackThreads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Feedback not found.");
        if (!string.IsNullOrWhiteSpace(body))
        {
            if (body.Length > 4000) return Result.Fail("Replies can be at most 4000 characters.");
            var name = await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";
            db.FeedbackMessages.Add(new FeedbackMessage { ThreadId = id, FromHr = true, AuthorName = name, Body = body.Trim(), SentOn = await TodayAsync(ct) });
            t.Status = FeedbackStatus.Answered;
        }
        else if (!close) return Result.Fail("Write a reply.");
        if (close) t.Status = FeedbackStatus.Closed;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
