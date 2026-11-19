using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class PromotionInput
{
    [Display(Name = "Employee")] public int EmployeeId { get; set; }
    [Display(Name = "New job title")] public int? ToDesignationId { get; set; }
    [Range(1, 1_000_000_000), Display(Name = "New annual gross (₹)")] public decimal? ProposedAnnualGross { get; set; }
    [Display(Name = "Effective from")] public DateOnly EffectiveOn { get; set; }
    [Required, StringLength(4000, MinimumLength = 20), Display(Name = "Why")] public string Justification { get; set; } = string.Empty;
    [Display(Name = "Based on review")] public int? PerformanceReviewId { get; set; }
}

public sealed record PromotionRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string? FromTitle, string? ToTitle,
    decimal? CurrentGross, decimal? ProposedGross, DateOnly EffectiveOn, PromotionStatus Status, string ProposedBy, string? ApplyError)
{
    public decimal? RaisePercent => CurrentGross is > 0 && ProposedGross is decimal p ? Math.Round((p - CurrentGross.Value) / CurrentGross.Value * 100, 1) : null;
}

public sealed record PromotionView(PromotionCase Case, PromotionRow Row, string? ReviewName, int? ReviewRating, bool CanDecide, bool CanWithdraw, bool CanRetry);

public interface IPromotionService
{
    Task<Result<int>> ProposeAsync(int? me, bool isHr, PromotionInput input, CancellationToken ct = default);
    Task<IReadOnlyList<PromotionRow>> ListAsync(PromotionStatus? status, CancellationToken ct = default);
    /// <summary>Promotions the manager proposed or that concern people in their reporting line.</summary>
    Task<IReadOnlyList<PromotionRow>> ForManagerAsync(int me, CancellationToken ct = default);
    Task<PromotionView?> GetAsync(int id, int? me, bool isHr, CancellationToken ct = default);
    Task<Result> DecideAsync(int id, bool approve, string? note, CancellationToken ct = default);
    Task<Result> WithdrawAsync(int id, int? me, bool isHr, CancellationToken ct = default);
    /// <summary>Applies an approved promotion now (used for retries after a failure).</summary>
    Task<Result> ApplyAsync(int id, CancellationToken ct = default);
    /// <summary>Hourly job: applies approved promotions whose date has come.</summary>
    Task<int> ApplyDueAsync(CancellationToken ct = default);
    /// <summary>The employee's latest released review in the last year, to link a proposal to.</summary>
    Task<IReadOnlyList<(int Id, string Name, int? Rating)>> ReviewsForAsync(int employeeId, CancellationToken ct = default);
}

public sealed class PromotionService(IAppDbContext db, ICurrentContext context, IClock clock, IPayrollSetupService payroll, INotificationService notifications,
    IAuditWriter audit) : IPromotionService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private IQueryable<PromotionRow> Rows(IQueryable<PromotionCase> q) =>
        from p in q
        join e in db.Employees on p.EmployeeId equals e.Id
        join f in db.Designations on p.FromDesignationId equals f.Id into fs
        from fd in fs.DefaultIfEmpty()
        join t in db.Designations on p.ToDesignationId equals t.Id into ts
        from td in ts.DefaultIfEmpty()
        select new PromotionRow(p.Id, e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode, fd == null ? null : fd.Name, td == null ? null : td.Name,
            p.CurrentAnnualGross, p.ProposedAnnualGross, p.EffectiveOn, p.Status, p.ProposedByName, p.ApplyError);

    public async Task<Result<int>> ProposeAsync(int? me, bool isHr, PromotionInput i, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == i.EmployeeId, ct);
        if (emp is not { IsCurrent: true }) return Result<int>.Fail("Choose a current employee.");
        if (!isHr && (me is not int m || !(await ReportingLine.LoadAsync(db, ct)).IsAbove(m, emp.Id)))
            return Result<int>.Fail("You can propose promotions only for people in your team.");
        if (me == emp.Id) return Result<int>.Fail("You can't propose your own promotion.");
        var titleChange = i.ToDesignationId is int to && to != emp.DesignationId;
        if (i.ToDesignationId is int d && !await db.Designations.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Job title not found.");
        var today = await TodayAsync(ct);
        var salary = await payroll.SalaryAsOfAsync(emp.Id, today, ct);
        var salaryChange = i.ProposedAnnualGross is decimal g && g != salary?.AnnualGross;
        if (!titleChange && !salaryChange) return Result<int>.Fail("Choose a new job title or a new salary.");
        if (salaryChange && salary is null) return Result<int>.Fail("This employee has no salary set up yet. Assign one under Payroll first.");
        if (i.EffectiveOn < today.AddDays(-90) || i.EffectiveOn > today.AddYears(1)) return Result<int>.Fail("The effective date must be within the last 90 days or the next year.");
        if (await db.PromotionCases.AnyAsync(p => p.EmployeeId == emp.Id && (p.Status == PromotionStatus.Proposed || p.Status == PromotionStatus.Approved), ct))
            return Result<int>.Fail("There's already an open promotion for this employee.");
        if (i.PerformanceReviewId is int r && !await db.PerformanceReviews.AnyAsync(x => x.Id == r && x.EmployeeId == emp.Id, ct))
            return Result<int>.Fail("That review isn't this employee's.");

        var c = new PromotionCase
        {
            EmployeeId = emp.Id, ProposedByEmployeeId = isHr ? null : me, ProposedByName = await MyNameAsync(ct),
            FromDesignationId = emp.DesignationId, ToDesignationId = titleChange ? i.ToDesignationId : null,
            CurrentAnnualGross = salary?.AnnualGross, ProposedAnnualGross = salaryChange ? i.ProposedAnnualGross : null,
            EffectiveOn = i.EffectiveOn, Justification = i.Justification.Trim(), PerformanceReviewId = i.PerformanceReviewId
        };
        db.PromotionCases.Add(c);
        await db.SaveChangesAsync(ct);
        if (!isHr)
        {
            var hr = await notifications.UsersWithPermissionAsync(TenantPermissions.PromotionsManage, ct);
            if (hr.Count > 0) notifications.Queue(hr, "Promotion proposed", $"{c.ProposedByName} proposes a promotion for {emp.FullName}.", $"/org/promotions/{c.Id}");
            await db.SaveChangesAsync(ct);
        }
        return Result<int>.Ok(c.Id);
    }

    public async Task<IReadOnlyList<PromotionRow>> ListAsync(PromotionStatus? status, CancellationToken ct = default)
    {
        var q = db.PromotionCases.AsNoTracking();
        if (status is { } s) q = q.Where(p => p.Status == s);
        return (await Rows(q).ToListAsync(ct)).OrderBy(r => r.Status).ThenBy(r => r.EffectiveOn).ToList();
    }

    public async Task<IReadOnlyList<PromotionRow>> ForManagerAsync(int me, CancellationToken ct = default)
    {
        var team = (await ReportingLine.LoadAsync(db, ct)).ReportsOf(me).ToList();
        return (await Rows(db.PromotionCases.AsNoTracking().Where(p => p.ProposedByEmployeeId == me || team.Contains(p.EmployeeId))).ToListAsync(ct))
            .OrderByDescending(r => r.EffectiveOn).ToList();
    }

    public async Task<PromotionView?> GetAsync(int id, int? me, bool isHr, CancellationToken ct = default)
    {
        var c = await db.PromotionCases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (c is null) return null;
        var isManager = me is int m && (c.ProposedByEmployeeId == m || (await ReportingLine.LoadAsync(db, ct)).IsAbove(m, c.EmployeeId));
        if (!isHr && !isManager) return null;
        var row = await Rows(db.PromotionCases.AsNoTracking().Where(p => p.Id == id)).FirstAsync(ct);
        var review = c.PerformanceReviewId is int r
            ? await db.PerformanceReviews.AsNoTracking().Where(x => x.Id == r).Select(x => new { x.Cycle.Name, Rating = x.FinalRating ?? x.OverallRating }).FirstOrDefaultAsync(ct)
            : null;
        return new PromotionView(c, row, review?.Name, review?.Rating,
            CanDecide: isHr && c.Status == PromotionStatus.Proposed,
            CanWithdraw: c.Status == PromotionStatus.Proposed && (isHr || c.ProposedByEmployeeId == me),
            CanRetry: isHr && c.Status == PromotionStatus.Approved && c.ApplyError is not null);
    }

    public async Task<Result> DecideAsync(int id, bool approve, string? note, CancellationToken ct = default)
    {
        var c = await db.PromotionCases.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (c is null || c.Status != PromotionStatus.Proposed) return Result.Fail("This promotion has already been decided.");
        if (!approve && string.IsNullOrWhiteSpace(note)) return Result.Fail("Give a reason for rejecting it.");
        c.Status = approve ? PromotionStatus.Approved : PromotionStatus.Rejected;
        c.DecidedByName = await MyNameAsync(ct);
        c.DecidedAtUtc = clock.UtcNow;
        c.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        audit.Add(approve ? "Promotion.Approved" : "Promotion.Rejected", nameof(PromotionCase), c.Id.ToString(), c.DecisionNote);
        await db.SaveChangesAsync(ct);

        var who = await db.Employees.Where(e => e.Id == c.EmployeeId).Select(e => new { e.UserId, Name = e.FirstName + " " + e.LastName }).FirstAsync(ct);
        if (c.ProposedByEmployeeId is int pb && await db.Employees.Where(e => e.Id == pb).Select(e => e.UserId).FirstOrDefaultAsync(ct) is int pu)
            notifications.Queue([pu], approve ? "Promotion approved" : "Promotion not approved", $"{who.Name}: {(approve ? $"effective {c.EffectiveOn:dd MMM yyyy}" : c.DecisionNote)}", $"/me/promotions/{c.Id}");
        if (approve && who.UserId is int eu)
            notifications.Queue([eu], "Congratulations on your promotion!", $"Effective {c.EffectiveOn:dd MMM yyyy}. HR will share the details.", "/me");
        await db.SaveChangesAsync(ct);

        if (approve && c.EffectiveOn <= await TodayAsync(ct)) await ApplyAsync(c.Id, ct);
        return Result.Ok();
    }

    public async Task<Result> WithdrawAsync(int id, int? me, bool isHr, CancellationToken ct = default)
    {
        var c = await db.PromotionCases.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (c is null || c.Status != PromotionStatus.Proposed || (!isHr && c.ProposedByEmployeeId != me)) return Result.Fail("Only an undecided proposal can be withdrawn, by its proposer or HR.");
        c.Status = PromotionStatus.Withdrawn;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ApplyAsync(int id, CancellationToken ct = default)
    {
        var c = await db.PromotionCases.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (c is null || c.Status != PromotionStatus.Approved) return Result.Fail("Only an approved promotion can be applied.");
        var today = await TodayAsync(ct);
        if (c.EffectiveOn > today) return Result.Fail($"It applies automatically on {c.EffectiveOn:dd MMM yyyy}.");
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == c.EmployeeId, ct);
        if (emp is not { IsCurrent: true }) { c.ApplyError = "The employee is no longer with the company."; await db.SaveChangesAsync(ct); return Result.Fail(c.ApplyError); }

        if (c.ProposedAnnualGross is decimal gross)
        {
            var current = await payroll.SalaryAsOfAsync(emp.Id, c.EffectiveOn, ct) ?? await payroll.SalaryAsOfAsync(emp.Id, today, ct);
            if (current is null) { c.ApplyError = "No salary is set up for this employee; assign one under Payroll, then retry."; await db.SaveChangesAsync(ct); return Result.Fail(c.ApplyError); }
            if (current.AnnualGross != gross || current.EffectiveFrom != c.EffectiveOn)
            {
                var r = await payroll.AssignAsync(emp.Id, new SalaryAssignInput
                {
                    SalaryStructureId = current.SalaryStructureId, AnnualGross = gross, EffectiveFrom = c.EffectiveOn, PfApplicable = current.PfApplicable,
                    Remarks = $"Promotion #{c.Id}", PayArrears = c.EffectiveOn < today
                }, ct);
                if (!r.Succeeded) { c.ApplyError = r.Error is { Length: > 500 } e ? e[..500] : r.Error; await db.SaveChangesAsync(ct); return Result.Fail(r.Error!); }
            }
        }
        if (c.ToDesignationId is int to) emp.DesignationId = to;
        c.Status = PromotionStatus.Applied;
        c.AppliedAtUtc = clock.UtcNow;
        c.ApplyError = null;
        audit.Add("Promotion.Applied", nameof(PromotionCase), c.Id.ToString(), $"Employee {emp.EmployeeCode}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<int> ApplyDueAsync(CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var due = await db.PromotionCases.Where(p => p.Status == PromotionStatus.Approved && p.EffectiveOn <= today && p.ApplyError == null).Select(p => p.Id).ToListAsync(ct);
        var applied = 0;
        foreach (var id in due) if ((await ApplyAsync(id, ct)).Succeeded) applied++;
        return applied;
    }

    public async Task<IReadOnlyList<(int Id, string Name, int? Rating)>> ReviewsForAsync(int employeeId, CancellationToken ct = default)
    {
        var since = (await TodayAsync(ct)).AddYears(-1);
        return (await db.PerformanceReviews.AsNoTracking()
                .Where(r => r.EmployeeId == employeeId && r.ManagerSubmittedAtUtc != null && r.Cycle.PeriodEnd >= since)
                .OrderByDescending(r => r.Cycle.PeriodEnd).Select(r => new { r.Id, r.Cycle.Name, Rating = r.FinalRating ?? r.OverallRating }).ToListAsync(ct))
            .Select(r => (r.Id, r.Name, r.Rating)).ToList();
    }
}
