using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class FeedbackInput
{
    [Display(Name = "About")] public int AboutEmployeeId { get; set; }
    public FeedbackTone Tone { get; set; } = FeedbackTone.Appreciation;
    [Display(Name = "Who can read it")] public FeedbackVisibility Visibility { get; set; } = FeedbackVisibility.PersonAndManagers;
    [Required, StringLength(2000, MinimumLength = 10), Display(Name = "Feedback")] public string Body { get; set; } = string.Empty;
    /// <summary>Set when answering a request.</summary>
    public int? RequestId { get; set; }
}

public sealed class FeedbackRequestInput
{
    [Display(Name = "Feedback about")] public int AboutEmployeeId { get; set; }
    [Display(Name = "Ask")] public List<int> RecipientIds { get; set; } = [];
    [Required, StringLength(500, MinimumLength = 5)] public string Question { get; set; } = "What should I keep doing, and what could I do better?";
    [Display(Name = "Reply by")] public DateOnly? DueOn { get; set; }
}

public sealed record FeedbackRow(int Id, int AuthorId, string AuthorName, int AboutId, string AboutName, FeedbackTone Tone, FeedbackVisibility Visibility,
    string Body, DateTime AtUtc, string? Question);
public sealed record FeedbackAsk(int RecipientRowId, int RequestId, int RequestedById, string RequestedByName, int AboutId, string AboutName, string Question, DateOnly? DueOn, DateTime AtUtc);
public sealed record FeedbackRequestView(int Id, int AboutId, string AboutName, string Question, DateOnly? DueOn, DateTime AtUtc, IReadOnlyList<(string Name, FeedbackRequestStatus Status)> Recipients);

public interface IFeedbackService
{
    Task<Result> GiveAsync(int me, FeedbackInput input, CancellationToken ct = default);
    Task<Result<int>> RequestAsync(int me, FeedbackRequestInput input, CancellationToken ct = default);
    Task<Result> DeclineAsync(int recipientRowId, int me, CancellationToken ct = default);
    /// <summary>Feedback about me that I may read (not the managers-only notes).</summary>
    Task<IReadOnlyList<FeedbackRow>> ReceivedAsync(int me, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackRow>> GivenAsync(int me, CancellationToken ct = default);
    /// <summary>Feedback about people in my reporting line that managers may read.</summary>
    Task<IReadOnlyList<FeedbackRow>> AboutMyTeamAsync(int me, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackAsk>> ToAnswerAsync(int me, CancellationToken ct = default);
    /// <summary>Current employees in my reporting line (empty for non-managers).</summary>
    Task<IReadOnlyList<LookupItem>> TeamAsync(int me, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackRequestView>> MyRequestsAsync(int me, CancellationToken ct = default);
    /// <param name="asManager">True for the reviewer/HR view (person-and-managers + managers-only); false for the person's own view.</param>
    Task<IReadOnlyList<FeedbackRow>> ForPeriodAsync(int aboutEmployeeId, DateOnly from, DateOnly to, bool asManager, CancellationToken ct = default);
}

public sealed class FeedbackService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IFeedbackService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    private IQueryable<FeedbackRow> Rows(IQueryable<FeedbackNote> q) =>
        from n in q
        join a in db.Employees on n.AuthorEmployeeId equals a.Id
        join p in db.Employees on n.AboutEmployeeId equals p.Id
        join r in db.FeedbackRequests on n.FeedbackRequestId equals r.Id into rs
        from req in rs.DefaultIfEmpty()
        select new FeedbackRow(n.Id, a.Id, a.FirstName + " " + a.LastName, p.Id, p.FirstName + " " + p.LastName, n.Tone, n.Visibility, n.Body, n.AtUtc, req == null ? null : req.Question);

    private static List<FeedbackRow> Newest(IEnumerable<FeedbackRow> rows) => rows.OrderByDescending(r => r.AtUtc).ToList();

    private async Task<Employee?> CurrentAsync(int id, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is { IsCurrent: true } ? e : null;
    }

    public async Task<Result> GiveAsync(int me, FeedbackInput i, CancellationToken ct = default)
    {
        if (i.AboutEmployeeId == me) return Result.Fail("Feedback is for colleagues. Use a self-review or your goals for yourself.");
        var about = await CurrentAsync(i.AboutEmployeeId, ct);
        if (about is null) return Result.Fail("Choose a current colleague.");
        var body = (i.Body ?? "").Trim();
        if (body.Length < 10) return Result.Fail("Write at least a sentence.");

        FeedbackRequestRecipient? answering = null;
        if (i.RequestId is int requestId)
        {
            var req = await db.FeedbackRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, ct);
            answering = await db.FeedbackRequestRecipients.FirstOrDefaultAsync(r => r.FeedbackRequestId == requestId && r.EmployeeId == me, ct);
            if (req is null || answering is null || answering.Status != FeedbackRequestStatus.Pending) return Result.Fail("This request has already been answered or withdrawn.");
            if (req.AboutEmployeeId != i.AboutEmployeeId) return Result.Fail("The answer must be about the person in the request.");
        }

        db.FeedbackNotes.Add(new FeedbackNote
        {
            AuthorEmployeeId = me, AboutEmployeeId = about.Id, Tone = i.Tone, Visibility = i.Visibility, Body = body,
            FeedbackRequestId = i.RequestId, AtUtc = clock.UtcNow
        });
        if (answering is not null) { answering.Status = FeedbackRequestStatus.Answered; answering.RespondedAtUtc = clock.UtcNow; }
        await db.SaveChangesAsync(ct);

        var author = await db.Employees.Where(e => e.Id == me).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
        if (i.Visibility != FeedbackVisibility.ManagersOnly && about.UserId is int u)
            notifications.Queue([u], i.Tone == FeedbackTone.Appreciation ? "You received appreciation" : "You received feedback", $"From {author}.", "/me/feedback-hub");
        if (answering is not null)
        {
            var asker = await db.FeedbackRequests.Where(r => r.Id == i.RequestId).Select(r => r.RequestedByEmployeeId).FirstAsync(ct);
            var askerUser = await db.Employees.Where(e => e.Id == asker && e.Id != about.Id).Select(e => e.UserId).FirstOrDefaultAsync(ct);
            if (askerUser is int au) notifications.Queue([au], "Feedback request answered", $"{author} answered your request about {about.FullName}.", "/me/feedback-hub?tab=team");
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> RequestAsync(int me, FeedbackRequestInput i, CancellationToken ct = default)
    {
        var line = await ReportingLine.LoadAsync(db, ct);
        if (i.AboutEmployeeId != me && !line.IsAbove(me, i.AboutEmployeeId))
            return Result<int>.Fail("You can ask for feedback about yourself or someone in your team.");
        var recipients = i.RecipientIds.Distinct().Where(r => r != i.AboutEmployeeId && r != me).ToList();
        if (recipients.Count == 0) return Result<int>.Fail("Choose at least one colleague to ask.");
        if (recipients.Count > 10) return Result<int>.Fail("Ask at most 10 people at a time.");
        var current = await db.Employees.AsNoTracking().Where(e => recipients.Contains(e.Id) && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .Select(e => new { e.Id, e.UserId }).ToListAsync(ct);
        if (current.Count != recipients.Count) return Result<int>.Fail("Some of the chosen people are no longer with the company.");
        var today = await TodayAsync(ct);
        if (i.DueOn is { } due && (due < today || due > today.AddDays(90))) return Result<int>.Fail("The reply-by date must be within the next 90 days.");

        var request = new FeedbackRequest { RequestedByEmployeeId = me, AboutEmployeeId = i.AboutEmployeeId, Question = i.Question.Trim(), DueOn = i.DueOn, AtUtc = clock.UtcNow };
        foreach (var r in recipients) request.Recipients.Add(new FeedbackRequestRecipient { EmployeeId = r });
        db.FeedbackRequests.Add(request);
        await db.SaveChangesAsync(ct);

        var names = await db.Employees.Where(e => e.Id == me || e.Id == i.AboutEmployeeId).Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var users = current.Where(c => c.UserId is not null).Select(c => c.UserId!.Value).ToList();
        if (users.Count > 0)
            notifications.Queue(users, "Feedback requested", i.AboutEmployeeId == me ? $"{names[me]} would value your feedback." : $"{names[me]} asks for your feedback about {names[i.AboutEmployeeId]}.", "/me/feedback-hub?tab=requests");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(request.Id);
    }

    public async Task<Result> DeclineAsync(int recipientRowId, int me, CancellationToken ct = default)
    {
        var row = await db.FeedbackRequestRecipients.FirstOrDefaultAsync(r => r.Id == recipientRowId && r.EmployeeId == me, ct);
        if (row is null || row.Status != FeedbackRequestStatus.Pending) return Result.Fail("This request isn't waiting for you.");
        row.Status = FeedbackRequestStatus.Declined;
        row.RespondedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<FeedbackRow>> ReceivedAsync(int me, CancellationToken ct = default) =>
        Newest(await Rows(db.FeedbackNotes.AsNoTracking().Where(n => n.AboutEmployeeId == me && n.Visibility != FeedbackVisibility.ManagersOnly)).ToListAsync(ct));

    public async Task<IReadOnlyList<FeedbackRow>> GivenAsync(int me, CancellationToken ct = default) =>
        Newest(await Rows(db.FeedbackNotes.AsNoTracking().Where(n => n.AuthorEmployeeId == me)).ToListAsync(ct));

    public async Task<IReadOnlyList<FeedbackRow>> AboutMyTeamAsync(int me, CancellationToken ct = default)
    {
        var team = (await ReportingLine.LoadAsync(db, ct)).ReportsOf(me).ToList();
        if (team.Count == 0) return [];
        return Newest(await Rows(db.FeedbackNotes.AsNoTracking()
            .Where(n => team.Contains(n.AboutEmployeeId) && n.Visibility != FeedbackVisibility.Private)).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<FeedbackAsk>> ToAnswerAsync(int me, CancellationToken ct = default) =>
        (await (from r in db.FeedbackRequestRecipients.AsNoTracking().Where(x => x.EmployeeId == me && x.Status == FeedbackRequestStatus.Pending)
                join q in db.FeedbackRequests on r.FeedbackRequestId equals q.Id
                join asker in db.Employees on q.RequestedByEmployeeId equals asker.Id
                join about in db.Employees on q.AboutEmployeeId equals about.Id
                where about.Status != EmployeeStatus.Exited && about.Status != EmployeeStatus.Inactive
                select new FeedbackAsk(r.Id, q.Id, asker.Id, asker.FirstName + " " + asker.LastName, about.Id, about.FirstName + " " + about.LastName, q.Question, q.DueOn, q.AtUtc))
            .ToListAsync(ct)).OrderBy(a => a.DueOn ?? DateOnly.MaxValue).ThenBy(a => a.AtUtc).ToList();

    public async Task<IReadOnlyList<LookupItem>> TeamAsync(int me, CancellationToken ct = default)
    {
        var team = (await ReportingLine.LoadAsync(db, ct)).ReportsOf(me).ToList();
        return team.Count == 0 ? [] : await db.Employees.AsNoTracking()
            .Where(e => team.Contains(e.Id) && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .OrderBy(e => e.FirstName).Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FeedbackRequestView>> MyRequestsAsync(int me, CancellationToken ct = default)
    {
        var requests = await db.FeedbackRequests.AsNoTracking().Include(r => r.Recipients).Where(r => r.RequestedByEmployeeId == me)
            .OrderByDescending(r => r.Id).Take(50).ToListAsync(ct);
        var ids = requests.SelectMany(r => r.Recipients.Select(x => x.EmployeeId)).Concat(requests.Select(r => r.AboutEmployeeId)).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return requests.Select(r => new FeedbackRequestView(r.Id, r.AboutEmployeeId, names.GetValueOrDefault(r.AboutEmployeeId, "—"), r.Question, r.DueOn, r.AtUtc,
            r.Recipients.Where(x => !x.IsDeleted).Select(x => (names.GetValueOrDefault(x.EmployeeId, "—"), x.Status)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<FeedbackRow>> ForPeriodAsync(int aboutEmployeeId, DateOnly from, DateOnly to, bool asManager, CancellationToken ct = default)
    {
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var end = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc).AddDays(1);
        var q = db.FeedbackNotes.AsNoTracking().Where(n => n.AboutEmployeeId == aboutEmployeeId && n.AtUtc >= start && n.AtUtc <= end);
        q = asManager ? q.Where(n => n.Visibility != FeedbackVisibility.Private) : q.Where(n => n.Visibility != FeedbackVisibility.ManagersOnly);
        return Newest(await Rows(q).ToListAsync(ct));
    }
}
