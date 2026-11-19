using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class ReviewCycleInput : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Display(Name = "Review period from")] public DateOnly PeriodStart { get; set; }
    [Display(Name = "to")] public DateOnly PeriodEnd { get; set; }
    [Display(Name = "Self-reviews due")] public DateOnly SelfReviewDue { get; set; }
    [Display(Name = "Manager reviews due")] public DateOnly ManagerReviewDue { get; set; }
    [Display(Name = "Show goals in the review")] public bool IncludeGoals { get; set; } = true;
    [Display(Name = "No self-review (managers only)")] public bool SkipSelfReview { get; set; }
    [Display(Name = "Only employees who joined on or before")] public DateOnly? JoinedOnOrBefore { get; set; }
    [Display(Name = "Only this department")] public int? DepartmentId { get; set; }
    [Required, StringLength(400), Display(Name = "Rating labels (1 to 5, separated by |)")]
    public string RatingLabels { get; set; } = "Needs improvement|Below expectations|Meets expectations|Exceeds expectations|Outstanding";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PeriodEnd < PeriodStart) yield return new ValidationResult("The period ends before it starts.", [nameof(PeriodEnd)]);
        if (!SkipSelfReview && ManagerReviewDue < SelfReviewDue) yield return new ValidationResult("Manager reviews can't be due before self-reviews.", [nameof(ManagerReviewDue)]);
        var labels = (RatingLabels ?? "").Split('|', StringSplitOptions.TrimEntries);
        if (labels.Length != 5 || labels.Any(l => l.Length == 0 || l.Length > 60))
            yield return new ValidationResult("Enter exactly five rating labels separated by |, lowest first.", [nameof(RatingLabels)]);
    }
}

public sealed class ReviewQuestionInput
{
    [Required, StringLength(500)] public string Text { get; set; } = string.Empty;
    public ReviewQuestionType Type { get; set; } = ReviewQuestionType.Rating;
    public ReviewAudience Audience { get; set; } = ReviewAudience.Both;
    public bool Required { get; set; } = true;
}

public sealed class AnswerInput
{
    public int QuestionId { get; set; }
    [Range(1, 5)] public int? Rating { get; set; }
    [StringLength(4000)] public string? Text { get; set; }
}

public sealed record ReviewCycleRow(int Id, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, ReviewCycleStatus Status, int Total, int SelfPending, int ManagerPending, int Submitted, int Released, int Acknowledged);
public sealed record ReviewRow(int Id, int CycleId, string CycleName, ReviewCycleStatus CycleStatus, int EmployeeId, string EmployeeName, string EmployeeCode,
    string? ReviewerName, ReviewStatus Status, int? Rating, DateOnly SelfDue, DateOnly ManagerDue);
public sealed record ReviewCycleView(ReviewCycle Cycle, IReadOnlyList<ReviewRow> Reviews, IReadOnlyDictionary<int, int> RatingCounts, int EligibleNow);
public sealed record ReviewView(PerformanceReview Review, ReviewCycle Cycle, string EmployeeName, string? ReviewerName, IReadOnlyList<ReviewQuestion> Questions,
    IReadOnlyList<ReviewAnswer> SelfAnswers, IReadOnlyList<ReviewAnswer> ManagerAnswers, IReadOnlyList<GoalRow> Goals, IReadOnlyList<FeedbackRow> Feedback, IReadOnlyList<ValueCount> Values,
    bool IsEmployee, bool IsReviewer, bool CanEditSelf, bool CanEditManager, bool ShowSelf, bool ShowManager, bool CanAcknowledge, bool CanAdjust)
{
    public IReadOnlyList<string> Labels => Cycle.RatingLabels.Split('|', StringSplitOptions.TrimEntries);
    public int? EffectiveRating => Review.FinalRating ?? Review.OverallRating;
}

public interface IReviewService
{
    Task<IReadOnlyList<ReviewCycleRow>> CyclesAsync(CancellationToken ct = default);
    Task<ReviewCycleView?> CycleAsync(int id, CancellationToken ct = default);
    Task<ReviewCycleInput?> GetCycleForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveCycleAsync(int? id, ReviewCycleInput input, CancellationToken ct = default);
    Task<Result> DeleteCycleAsync(int id, CancellationToken ct = default);
    Task<Result> AddQuestionAsync(int cycleId, ReviewQuestionInput input, CancellationToken ct = default);
    Task<Result> RemoveQuestionAsync(int questionId, CancellationToken ct = default);
    /// <summary>Adds a rating question for each active core value not already in the form. Returns how many were added.</summary>
    Task<Result<int>> AddValueQuestionsAsync(int cycleId, CancellationToken ct = default);
    Task<Result<int>> LaunchAsync(int cycleId, CancellationToken ct = default);
    Task<Result<int>> ReleaseAsync(int cycleId, CancellationToken ct = default);
    Task<Result> CloseAsync(int cycleId, CancellationToken ct = default);
    Task<byte[]?> ExportAsync(int cycleId, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewRow>> MineAsync(int employeeId, CancellationToken ct = default);
    /// <summary>Reviews the viewer must write: as reporting manager, or (with HR rights) for employees without a manager.</summary>
    Task<IReadOnlyList<ReviewRow>> ToWriteAsync(int? reviewerEmployeeId, bool isHr, CancellationToken ct = default);
    Task<ReviewView?> GetAsync(int id, int? myEmployeeId, bool isHr, CancellationToken ct = default);
    Task<Result> SaveSelfAsync(int id, int employeeId, IReadOnlyList<AnswerInput> answers, bool submit, CancellationToken ct = default);
    Task<Result> SaveManagerAsync(int id, int? myEmployeeId, bool isHr, IReadOnlyList<AnswerInput> answers, int? overallRating, bool submit, CancellationToken ct = default);
    Task<Result> AdjustAsync(int id, int finalRating, string? note, CancellationToken ct = default);
    Task<Result> AcknowledgeAsync(int id, int employeeId, string? comment, CancellationToken ct = default);
}

public sealed class ReviewService(IAppDbContext db, ICurrentContext context, IClock clock, IGoalService goals, INotificationService notifications,
    IExcelExporter excel, IAuditWriter audit, IFeedbackService feedback, ICoreValueService values) : IReviewService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public static IReadOnlyList<ReviewQuestionInput> DefaultQuestions =>
    [
        new() { Text = "Quality of work: accuracy, thoroughness and standards", Type = ReviewQuestionType.Rating },
        new() { Text = "Ownership: takes responsibility and delivers on commitments", Type = ReviewQuestionType.Rating },
        new() { Text = "Collaboration and communication", Type = ReviewQuestionType.Rating },
        new() { Text = "Key achievements in this period", Type = ReviewQuestionType.Text },
        new() { Text = "Areas to develop, and support needed", Type = ReviewQuestionType.Text },
        new() { Text = "Overall comments", Type = ReviewQuestionType.Text, Audience = ReviewAudience.ManagerOnly, Required = false },
    ];

    private static bool AppliesTo(ReviewQuestion q, ReviewRole role) =>
        q.Audience == ReviewAudience.Both || (role == ReviewRole.Self ? q.Audience == ReviewAudience.SelfOnly : q.Audience == ReviewAudience.ManagerOnly);

    // ------------------------------------------------------------------ cycles (HR)

    private IQueryable<ReviewRow> Rows(IQueryable<PerformanceReview> q) =>
        from r in q
        join e in db.Employees on r.EmployeeId equals e.Id
        join m in db.Employees on r.ReviewerEmployeeId equals m.Id into ms
        from mg in ms.DefaultIfEmpty()
        select new ReviewRow(r.Id, r.ReviewCycleId, r.Cycle.Name, r.Cycle.Status, e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode,
            mg == null ? null : mg.FirstName + " " + mg.LastName, r.Status, r.FinalRating ?? r.OverallRating, r.Cycle.SelfReviewDue, r.Cycle.ManagerReviewDue);

    public async Task<IReadOnlyList<ReviewCycleRow>> CyclesAsync(CancellationToken ct = default)
    {
        var cycles = await db.ReviewCycles.AsNoTracking().OrderByDescending(c => c.PeriodEnd).ToListAsync(ct);
        var counts = await db.PerformanceReviews.AsNoTracking().GroupBy(r => new { r.ReviewCycleId, r.Status })
            .Select(g => new { g.Key.ReviewCycleId, g.Key.Status, N = g.Count() }).ToListAsync(ct);
        int N(int id, ReviewStatus s) => counts.Where(c => c.ReviewCycleId == id && c.Status == s).Sum(c => c.N);
        return cycles.Select(c => new ReviewCycleRow(c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status, counts.Where(x => x.ReviewCycleId == c.Id).Sum(x => x.N),
            N(c.Id, ReviewStatus.SelfReview), N(c.Id, ReviewStatus.ManagerReview), N(c.Id, ReviewStatus.Submitted), N(c.Id, ReviewStatus.Released), N(c.Id, ReviewStatus.Acknowledged))).ToList();
    }

    public async Task<ReviewCycleView?> CycleAsync(int id, CancellationToken ct = default)
    {
        var cycle = await db.ReviewCycles.AsNoTracking().Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return null;
        var reviews = (await Rows(db.PerformanceReviews.AsNoTracking().Where(r => r.ReviewCycleId == id)).ToListAsync(ct)).OrderBy(r => r.EmployeeName).ToList();
        var ratings = reviews.Where(r => r.Rating is not null).GroupBy(r => r.Rating!.Value).ToDictionary(g => g.Key, g => g.Count());
        var eligible = cycle.Status == ReviewCycleStatus.Draft ? (await EligibleAsync(cycle, ct)).Count : 0;
        return new ReviewCycleView(cycle, reviews, ratings, eligible);
    }

    private async Task<List<Employee>> EligibleAsync(ReviewCycle c, CancellationToken ct)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (c.JoinedOnOrBefore is { } j) q = q.Where(e => e.DateOfJoining <= j);
        if (c.DepartmentId is int d) q = q.Where(e => e.DepartmentId == d);
        return await q.ToListAsync(ct);
    }

    public async Task<ReviewCycleInput?> GetCycleForEditAsync(int id, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : new ReviewCycleInput
        {
            Name = c.Name, PeriodStart = c.PeriodStart, PeriodEnd = c.PeriodEnd, SelfReviewDue = c.SelfReviewDue, ManagerReviewDue = c.ManagerReviewDue,
            IncludeGoals = c.IncludeGoals, SkipSelfReview = c.SkipSelfReview, JoinedOnOrBefore = c.JoinedOnOrBefore, DepartmentId = c.DepartmentId, RatingLabels = c.RatingLabels
        };
    }

    public async Task<Result<int>> SaveCycleAsync(int? id, ReviewCycleInput i, CancellationToken ct = default)
    {
        ReviewCycle c;
        if (id is int existing)
        {
            c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new InvalidOperationException();
            if (c.Status is ReviewCycleStatus.Released or ReviewCycleStatus.Closed) return Result<int>.Fail("Results are released; the cycle can't be changed.");
            // After launch only the name and due dates can change (who is reviewed and the form are fixed).
            if (c.Status == ReviewCycleStatus.Active && (c.SkipSelfReview != i.SkipSelfReview || c.DepartmentId != i.DepartmentId || c.JoinedOnOrBefore != i.JoinedOnOrBefore
                    || c.PeriodStart != i.PeriodStart || c.PeriodEnd != i.PeriodEnd || c.RatingLabels != NormalizeLabels(i.RatingLabels)))
                return Result<int>.Fail("After launch only the name, due dates and the goals option can be changed.");
        }
        else
        {
            c = new ReviewCycle();
            db.ReviewCycles.Add(c);
            var order = 0;
            foreach (var q in DefaultQuestions)
                c.Questions.Add(new ReviewQuestion { Text = q.Text, Type = q.Type, Audience = q.Audience, Required = q.Required, SortOrder = order += 10 });
        }
        if (i.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Department not found.");
        c.Name = i.Name.Trim();
        c.PeriodStart = i.PeriodStart;
        c.PeriodEnd = i.PeriodEnd;
        c.SelfReviewDue = i.SkipSelfReview ? i.ManagerReviewDue : i.SelfReviewDue;
        c.ManagerReviewDue = i.ManagerReviewDue;
        c.IncludeGoals = i.IncludeGoals;
        c.SkipSelfReview = i.SkipSelfReview;
        c.JoinedOnOrBefore = i.JoinedOnOrBefore;
        c.DepartmentId = i.DepartmentId;
        c.RatingLabels = NormalizeLabels(i.RatingLabels);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(c.Id);
    }

    private static string NormalizeLabels(string labels) => string.Join("|", labels.Split('|', StringSplitOptions.TrimEntries));

    public async Task<Result> DeleteCycleAsync(int id, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Draft) return Result.Fail("Only a draft cycle can be deleted.");
        db.ReviewQuestions.RemoveRange(c.Questions);
        db.ReviewCycles.Remove(c);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddQuestionAsync(int cycleId, ReviewQuestionInput i, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (c is null) return Result.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Draft) return Result.Fail("Questions can be changed only before launch.");
        if (string.IsNullOrWhiteSpace(i.Text)) return Result.Fail("Enter the question.");
        if (c.Questions.Count(q => !q.IsDeleted) >= 30) return Result.Fail("A form can have at most 30 questions.");
        c.Questions.Add(new ReviewQuestion { Text = i.Text.Trim(), Type = i.Type, Audience = i.Audience, Required = i.Required, SortOrder = (c.Questions.Count == 0 ? 0 : c.Questions.Max(q => q.SortOrder)) + 10 });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveQuestionAsync(int questionId, CancellationToken ct = default)
    {
        var q = await db.ReviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId, ct);
        if (q is null) return Result.Fail("Question not found.");
        var c = await db.ReviewCycles.FirstAsync(x => x.Id == q.ReviewCycleId, ct);
        if (c.Status != ReviewCycleStatus.Draft) return Result.Fail("Questions can be changed only before launch.");
        db.ReviewQuestions.Remove(q);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public static string ValueQuestion(string valueName) => $"Lives our value: {valueName}";

    public async Task<Result<int>> AddValueQuestionsAsync(int cycleId, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (c is null) return Result<int>.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Draft) return Result<int>.Fail("Questions can be changed only before launch.");
        var active = (await values.ValuesAsync(false, 1, ct)).Select(v => ValueQuestion(v.Name)).ToList();
        if (active.Count == 0) return Result<int>.Fail("Define the company's core values first.");
        var existing = c.Questions.Where(q => !q.IsDeleted).Select(q => q.Text).ToHashSet();
        var order = c.Questions.Count == 0 ? 0 : c.Questions.Max(q => q.SortOrder);
        var added = 0;
        foreach (var text in active.Where(t => !existing.Contains(t)))
        {
            if (c.Questions.Count(q => !q.IsDeleted) + added >= 30) break;
            c.Questions.Add(new ReviewQuestion { Text = text, Type = ReviewQuestionType.Rating, Audience = ReviewAudience.Both, Required = true, SortOrder = order += 10 });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(added);
    }

    public async Task<Result<int>> LaunchAsync(int cycleId, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (c is null) return Result<int>.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Draft) return Result<int>.Fail("This cycle has already been launched.");
        var qs = c.Questions.Where(q => !q.IsDeleted).ToList();
        if (!qs.Any(q => AppliesTo(q, ReviewRole.Manager))) return Result<int>.Fail("Add at least one question for managers.");
        if (!c.SkipSelfReview && !qs.Any(q => AppliesTo(q, ReviewRole.Self))) return Result<int>.Fail("Add at least one self-review question, or choose 'No self-review'.");
        var today = await TodayAsync(ct);
        if (c.ManagerReviewDue < today) return Result<int>.Fail("The manager due date has passed. Move it to a later date.");

        var people = await EligibleAsync(c, ct);
        if (people.Count == 0) return Result<int>.Fail("No current employees match this cycle.");
        var currentIds = people.Select(p => p.Id).ToHashSet();
        var allCurrent = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive).Select(e => new { e.Id, e.UserId }).ToDictionaryAsync(e => e.Id, ct);
        foreach (var e in people)
        {
            // The reviewer is the reporting manager if they are still here; otherwise HR reviews.
            int? reviewer = e.ReportingManagerId is int m && allCurrent.ContainsKey(m) && m != e.Id ? m : null;
            db.PerformanceReviews.Add(new PerformanceReview
            {
                ReviewCycleId = c.Id, EmployeeId = e.Id, ReviewerEmployeeId = reviewer,
                Status = c.SkipSelfReview ? ReviewStatus.ManagerReview : ReviewStatus.SelfReview
            });
        }
        c.Status = ReviewCycleStatus.Active;
        c.LaunchedAtUtc = clock.UtcNow;
        audit.Add("Review.CycleLaunched", nameof(ReviewCycle), c.Id.ToString(), $"{c.Name}: {people.Count} employee(s)");
        await db.SaveChangesAsync(ct);

        if (!c.SkipSelfReview)
        {
            var users = people.Where(p => p.UserId is not null).Select(p => p.UserId!.Value).ToList();
            if (users.Count > 0) notifications.Queue(users, "Your self-review is open", $"{c.Name}: please complete it by {c.SelfReviewDue:dd MMM yyyy}.", "/me/reviews");
        }
        var reviewers = people.Where(p => p.ReportingManagerId is int m && allCurrent.ContainsKey(m)).Select(p => allCurrent[p.ReportingManagerId!.Value].UserId).OfType<int>().Distinct().ToList();
        if (reviewers.Count > 0) notifications.Queue(reviewers, "Team reviews to write", $"{c.Name}: manager reviews are due by {c.ManagerReviewDue:dd MMM yyyy}.", "/me/reviews");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(people.Count);
    }

    public async Task<Result<int>> ReleaseAsync(int cycleId, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (c is null) return Result<int>.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Active) return Result<int>.Fail("Only an active cycle can be released.");
        var done = await db.PerformanceReviews.Where(r => r.ReviewCycleId == cycleId && r.Status == ReviewStatus.Submitted).ToListAsync(ct);
        if (done.Count == 0) return Result<int>.Fail("No manager review has been submitted yet.");
        foreach (var r in done) r.Status = ReviewStatus.Released;
        c.Status = ReviewCycleStatus.Released;
        c.ReleasedAtUtc = clock.UtcNow;
        audit.Add("Review.Released", nameof(ReviewCycle), c.Id.ToString(), $"{c.Name}: {done.Count} result(s) shared");
        await db.SaveChangesAsync(ct);
        var ids = done.Select(r => r.EmployeeId).ToList();
        var users = await db.Employees.Where(e => ids.Contains(e.Id) && e.UserId != null).Select(e => e.UserId!.Value).ToListAsync(ct);
        if (users.Count > 0) notifications.Queue(users, "Your review is ready", $"{c.Name}: read your manager's review and acknowledge it.", "/me/reviews");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(done.Count);
    }

    public async Task<Result> CloseAsync(int cycleId, CancellationToken ct = default)
    {
        var c = await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (c is null) return Result.Fail("Cycle not found.");
        if (c.Status != ReviewCycleStatus.Released) return Result.Fail("Release the results before closing the cycle.");
        c.Status = ReviewCycleStatus.Closed;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<byte[]?> ExportAsync(int cycleId, CancellationToken ct = default)
    {
        var view = await CycleAsync(cycleId, ct);
        if (view is null) return null;
        var labels = view.Cycle.RatingLabels.Split('|');
        var ids = view.Reviews.Select(r => r.Id).ToList();
        var extra = await db.PerformanceReviews.AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.OverallRating, r.FinalRating, r.AdjustmentNote, r.ReviewedByName, r.AcknowledgedAtUtc }).ToDictionaryAsync(r => r.Id, ct);
        return excel.Export("Reviews", ["Employee code", "Employee", "Reviewer", "Status", "Manager rating", "Final rating", "Final rating label", "Adjustment note", "Reviewed by", "Acknowledged"],
            view.Reviews.Select(r =>
            {
                var x = extra[r.Id];
                var final = x.FinalRating ?? x.OverallRating;
                return (IReadOnlyList<object?>)[r.EmployeeCode, r.EmployeeName, r.ReviewerName ?? "HR", r.Status.ToString(), x.OverallRating, final,
                    final is int f ? labels[f - 1] : null, x.AdjustmentNote, x.ReviewedByName, x.AcknowledgedAtUtc?.ToString("dd-MM-yyyy")];
            }));
    }

    // ------------------------------------------------------------------ employees & reviewers

    public async Task<IReadOnlyList<ReviewRow>> MineAsync(int employeeId, CancellationToken ct = default) =>
        (await Rows(db.PerformanceReviews.AsNoTracking().Where(r => r.EmployeeId == employeeId && r.Cycle.Status != ReviewCycleStatus.Draft)).ToListAsync(ct))
            .OrderByDescending(r => r.ManagerDue).ToList();

    public async Task<IReadOnlyList<ReviewRow>> ToWriteAsync(int? reviewerEmployeeId, bool isHr, CancellationToken ct = default)
    {
        var q = db.PerformanceReviews.AsNoTracking().Where(r => r.Cycle.Status == ReviewCycleStatus.Active || r.Cycle.Status == ReviewCycleStatus.Released);
        q = q.Where(r => (reviewerEmployeeId != null && r.ReviewerEmployeeId == reviewerEmployeeId) || (isHr && r.ReviewerEmployeeId == null));
        return (await Rows(q).ToListAsync(ct)).OrderBy(r => r.Status is ReviewStatus.Submitted or ReviewStatus.Released or ReviewStatus.Acknowledged).ThenBy(r => r.EmployeeName).ToList();
    }

    public async Task<ReviewView?> GetAsync(int id, int? me, bool isHr, CancellationToken ct = default)
    {
        var r = await db.PerformanceReviews.AsNoTracking().Include(x => x.Cycle).ThenInclude(c => c.Questions).Include(x => x.Answers).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Cycle.Status == ReviewCycleStatus.Draft) return null;
        var isEmployee = me == r.EmployeeId;
        var isReviewer = (me is not null && me == r.ReviewerEmployeeId) || (isHr && r.ReviewerEmployeeId is null);
        if (!isEmployee && !isReviewer && !isHr) return null;

        var today = await TodayAsync(ct);
        var names = await db.Employees.AsNoTracking().Where(e => e.Id == r.EmployeeId || e.Id == r.ReviewerEmployeeId)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var cycleOpen = r.Cycle.Status is ReviewCycleStatus.Active or ReviewCycleStatus.Released;
        var selfDone = r.SelfSubmittedAtUtc is not null;
        var selfMissed = !selfDone && today > r.Cycle.SelfReviewDue;
        var managerDone = r.ManagerSubmittedAtUtc is not null;
        var released = r.Status is ReviewStatus.Released or ReviewStatus.Acknowledged;

        IReadOnlyList<GoalRow> goalRows = [];
        if (r.Cycle.IncludeGoals)
        {
            var viewer = new GoalViewer(r.EmployeeId, true, new HashSet<int>()); // already authorised above
            goalRows = await goals.IndividualAsync(r.EmployeeId, viewer, r.Cycle.PeriodStart, r.Cycle.PeriodEnd, ct);
        }
        var questions = r.Cycle.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ToList();
        var answers = r.Answers.Where(a => !a.IsDeleted).ToList();
        return new ReviewView(r, r.Cycle, names.GetValueOrDefault(r.EmployeeId, "Employee"), r.ReviewerEmployeeId is int rv ? names.GetValueOrDefault(rv) : null, questions,
            answers.Where(a => a.Role == ReviewRole.Self).ToList(), answers.Where(a => a.Role == ReviewRole.Manager).ToList(), goalRows,
            // Feedback received in the period: the person sees what they may read; reviewers and HR also see managers-only notes.
            await feedback.ForPeriodAsync(r.EmployeeId, r.Cycle.PeriodStart, r.Cycle.PeriodEnd, asManager: !isEmployee, ct),
            await values.ForPeriodAsync(r.EmployeeId, r.Cycle.PeriodStart, r.Cycle.PeriodEnd, ct),
            isEmployee, isReviewer,
            CanEditSelf: isEmployee && cycleOpen && !r.Cycle.SkipSelfReview && r.Status == ReviewStatus.SelfReview,
            CanEditManager: isReviewer && cycleOpen && !managerDone && (r.Cycle.SkipSelfReview || selfDone || selfMissed),
            ShowSelf: isEmployee || selfDone,
            ShowManager: isReviewer || isHr || (isEmployee && released),
            CanAcknowledge: isEmployee && r.Status == ReviewStatus.Released,
            CanAdjust: isHr && r.Cycle.Status == ReviewCycleStatus.Active && r.Status == ReviewStatus.Submitted);
    }

    private static string? Check(IReadOnlyList<ReviewQuestion> questions, ReviewRole role, IReadOnlyList<AnswerInput> answers)
    {
        foreach (var q in questions.Where(q => AppliesTo(q, role) && q.Required))
        {
            var a = answers.FirstOrDefault(x => x.QuestionId == q.Id);
            if (q.Type == ReviewQuestionType.Rating && a?.Rating is not (>= 1 and <= 5)) return $"Give a rating for: {q.Text}";
            if (q.Type == ReviewQuestionType.Text && string.IsNullOrWhiteSpace(a?.Text)) return $"Answer: {q.Text}";
        }
        return null;
    }

    private void Store(PerformanceReview r, IReadOnlyList<ReviewQuestion> questions, ReviewRole role, IReadOnlyList<AnswerInput> answers)
    {
        foreach (var q in questions.Where(q => AppliesTo(q, role)))
        {
            var input = answers.FirstOrDefault(a => a.QuestionId == q.Id);
            var existing = r.Answers.FirstOrDefault(a => a.ReviewQuestionId == q.Id && a.Role == role && !a.IsDeleted);
            if (existing is null) { existing = new ReviewAnswer { ReviewQuestionId = q.Id, Role = role }; r.Answers.Add(existing); }
            existing.Rating = q.Type == ReviewQuestionType.Rating && input?.Rating is >= 1 and <= 5 ? input.Rating : null;
            var text = input?.Text?.Trim();
            existing.Text = string.IsNullOrEmpty(text) ? null : text.Length > 4000 ? text[..4000] : text;
        }
    }

    public async Task<Result> SaveSelfAsync(int id, int employeeId, IReadOnlyList<AnswerInput> answers, bool submit, CancellationToken ct = default)
    {
        var view = await GetAsync(id, employeeId, false, ct);
        if (view is null || !view.CanEditSelf) return Result.Fail("This self-review can no longer be changed.");
        if (submit && Check(view.Questions, ReviewRole.Self, answers) is { } missing) return Result.Fail(missing);
        var r = await db.PerformanceReviews.Include(x => x.Answers).FirstAsync(x => x.Id == id, ct);
        Store(r, view.Questions, ReviewRole.Self, answers);
        if (submit)
        {
            r.Status = ReviewStatus.ManagerReview;
            r.SelfSubmittedAtUtc = clock.UtcNow;
            var to = r.ReviewerEmployeeId is int m
                ? await db.Employees.Where(e => e.Id == m && e.UserId != null).Select(e => e.UserId!.Value).ToListAsync(ct)
                : (await notifications.UsersWithPermissionAsync(TenantPermissions.ReviewsManage, ct)).ToList();
            if (to.Count > 0) notifications.Queue(to, "Self-review submitted", $"{view.EmployeeName} completed their self-review for {view.Cycle.Name}.", r.ReviewerEmployeeId is null ? $"/org/reviews/r/{r.Id}" : $"/me/reviews/{r.Id}");
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SaveManagerAsync(int id, int? me, bool isHr, IReadOnlyList<AnswerInput> answers, int? overallRating, bool submit, CancellationToken ct = default)
    {
        var view = await GetAsync(id, me, isHr, ct);
        if (view is null || !view.CanEditManager) return Result.Fail(view is { IsReviewer: true, Review.SelfSubmittedAtUtc: null } && view.Review.ManagerSubmittedAtUtc is null
            ? $"Wait for the self-review (due {view.Cycle.SelfReviewDue:dd MMM yyyy})." : "This review can no longer be changed.");
        if (submit && Check(view.Questions, ReviewRole.Manager, answers) is { } missing) return Result.Fail(missing);
        if (submit && overallRating is not (>= 1 and <= 5)) return Result.Fail("Choose an overall rating.");
        var r = await db.PerformanceReviews.Include(x => x.Answers).Include(x => x.Cycle).FirstAsync(x => x.Id == id, ct);
        Store(r, view.Questions, ReviewRole.Manager, answers);
        r.OverallRating = overallRating is >= 1 and <= 5 ? overallRating : null;
        if (submit)
        {
            r.ManagerSubmittedAtUtc = clock.UtcNow;
            r.ReviewedByName = await MyNameAsync(ct);
            // In an already released cycle a late review goes straight to the employee.
            r.Status = r.Cycle.Status == ReviewCycleStatus.Released ? ReviewStatus.Released : ReviewStatus.Submitted;
            if (r.Status == ReviewStatus.Released)
            {
                var user = await db.Employees.Where(e => e.Id == r.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
                if (user is int u) notifications.Queue([u], "Your review is ready", $"{r.Cycle.Name}: read your manager's review and acknowledge it.", $"/me/reviews/{r.Id}");
            }
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AdjustAsync(int id, int finalRating, string? note, CancellationToken ct = default)
    {
        var r = await db.PerformanceReviews.Include(x => x.Cycle).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Cycle.Status != ReviewCycleStatus.Active || r.Status != ReviewStatus.Submitted) return Result.Fail("Ratings can be adjusted only after the manager submits and before results are released.");
        if (finalRating is < 1 or > 5) return Result.Fail("Choose a rating from 1 to 5.");
        if (finalRating != r.OverallRating && string.IsNullOrWhiteSpace(note)) return Result.Fail("Say why the rating is being changed.");
        r.FinalRating = finalRating == r.OverallRating ? null : finalRating;
        r.AdjustmentNote = r.FinalRating is null ? null : note!.Trim() is { Length: > 500 } n ? n[..500] : note!.Trim();
        audit.Add("Review.RatingAdjusted", nameof(PerformanceReview), r.Id.ToString(), $"{r.OverallRating} → {finalRating}: {r.AdjustmentNote}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AcknowledgeAsync(int id, int employeeId, string? comment, CancellationToken ct = default)
    {
        var r = await db.PerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (r is null || r.Status != ReviewStatus.Released) return Result.Fail("This review isn't waiting for your acknowledgement.");
        r.Status = ReviewStatus.Acknowledged;
        r.AcknowledgedAtUtc = clock.UtcNow;
        r.EmployeeComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim() is { Length: > 2000 } c ? c[..2000] : comment.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
