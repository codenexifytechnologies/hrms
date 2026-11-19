using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

// ====================================================================== reminder rules

public sealed class ReminderRuleInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    public ReminderEvent Event { get; set; } = ReminderEvent.ProbationEnding;
    [Range(0, 90)] public int Days { get; set; } = 7;
    [Display(Name = "The employee")] public bool NotifyEmployee { get; set; }
    [Display(Name = "Their manager")] public bool NotifyManager { get; set; } = true;
    [Display(Name = "HR")] public bool NotifyHr { get; set; } = true;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed record ReminderRuleRow(int Id, string Name, ReminderEvent Event, int Days, bool NotifyEmployee, bool NotifyManager, bool NotifyHr, bool IsActive, int SentCount);

public interface IReminderService
{
    Task<IReadOnlyList<ReminderRuleRow>> ListAsync(CancellationToken ct = default);
    Task<ReminderRuleInput?> GetAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, ReminderRuleInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Evaluates active rules for today; each reminder is sent once per subject. Returns the reminders sent.</summary>
    Task<int> RunAsync(CancellationToken ct = default);
}

public sealed class ReminderService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IReminderService
{
    public async Task<IReadOnlyList<ReminderRuleRow>> ListAsync(CancellationToken ct = default) =>
        await db.NotificationRules.AsNoTracking().OrderBy(r => r.Event).ThenBy(r => r.Name)
            .Select(r => new ReminderRuleRow(r.Id, r.Name, r.Event, r.Days, r.NotifyEmployee, r.NotifyManager, r.NotifyHr, r.IsActive,
                db.NotificationRuleLogs.Count(l => l.RuleId == r.Id))).ToListAsync(ct);

    public async Task<ReminderRuleInput?> GetAsync(int id, CancellationToken ct = default) =>
        await db.NotificationRules.AsNoTracking().Where(r => r.Id == id).Select(r => new ReminderRuleInput
        {
            Name = r.Name, Event = r.Event, Days = r.Days, NotifyEmployee = r.NotifyEmployee, NotifyManager = r.NotifyManager, NotifyHr = r.NotifyHr, IsActive = r.IsActive
        }).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> SaveAsync(int? id, ReminderRuleInput i, CancellationToken ct = default)
    {
        if (!i.NotifyEmployee && !i.NotifyManager && !i.NotifyHr) return Result<int>.Fail("Choose who gets the reminder.");
        if (i.Days is < 0 or > 90) return Result<int>.Fail("Days must be between 0 and 90.");
        var rule = id is null ? db.NotificationRules.Add(new NotificationRule()).Entity : await db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return Result<int>.Fail("Rule not found.");
        rule.Name = i.Name.Trim(); rule.Event = i.Event; rule.Days = i.Days;
        rule.NotifyEmployee = i.NotifyEmployee; rule.NotifyManager = i.NotifyManager; rule.NotifyHr = i.NotifyHr; rule.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(rule.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var rule = await db.NotificationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return Result.Fail("Rule not found.");
        db.NotificationRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private sealed record Subject(string Key, int? EmployeeUserId, int? ManagerUserId, string HrText, string EmployeeText, string HrLink, string EmployeeLink);

    private IQueryable<Employee> Current => db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);

    private async Task<int?> ManagerUserAsync(int? managerId, CancellationToken ct) =>
        managerId is int m ? await db.Employees.Where(e => e.Id == m).Select(e => e.UserId).FirstOrDefaultAsync(ct) : null;

    private static bool SameDay(DateOnly date, DateOnly target) =>
        date.Month == target.Month && (date.Day == target.Day
            // Feb 29 dates are remembered on Feb 28 in other years.
            || (date.Month == 2 && date.Day == 29 && target.Day == 28 && !DateTime.IsLeapYear(target.Year)));

    private async Task<List<Subject>> SubjectsAsync(NotificationRule rule, DateOnly today, CancellationToken ct)
    {
        var target = today.AddDays(rule.Days);
        var list = new List<Subject>();
        switch (rule.Event)
        {
            case ReminderEvent.ProbationEnding:
                foreach (var e in await Current.Where(e => e.Status == EmployeeStatus.Probation && e.ProbationEndDate == target).ToListAsync(ct))
                    list.Add(new($"probation:{e.Id}:{target:yyyyMMdd}", e.UserId, await ManagerUserAsync(e.ReportingManagerId, ct),
                        $"{e.FullName}'s probation ends on {target:dd MMM yyyy}.", $"Your probation period ends on {target:dd MMM yyyy}.",
                        $"/org/employees/details/{e.Id}", "/me"));
                break;
            case ReminderEvent.DocumentExpiring:
                var docs = await db.EmployeeDocuments.AsNoTracking().Where(d => d.ExpiresOn == target)
                    .Join(Current, d => d.EmployeeId, e => e.Id, (d, e) => new { d.Id, d.Title, e }).ToListAsync(ct);
                foreach (var d in docs)
                    list.Add(new($"doc:{d.Id}:{target:yyyyMMdd}", d.e.UserId, await ManagerUserAsync(d.e.ReportingManagerId, ct),
                        $"{d.e.FullName}'s document “{d.Title}” expires on {target:dd MMM yyyy}.", $"Your document “{d.Title}” expires on {target:dd MMM yyyy}. Please share a renewed copy with HR.",
                        $"/org/employees/details/{d.e.Id}", "/me"));
                break;
            case ReminderEvent.WorkAnniversary:
                foreach (var e in (await Current.Where(e => e.DateOfJoining.Month == target.Month && e.DateOfJoining.Year < target.Year).ToListAsync(ct)).Where(e => SameDay(e.DateOfJoining, target)))
                {
                    var years = target.Year - e.DateOfJoining.Year;
                    list.Add(new($"anniversary:{e.Id}:{target.Year}", e.UserId, await ManagerUserAsync(e.ReportingManagerId, ct),
                        $"{e.FullName} completes {years} year{(years == 1 ? "" : "s")} with us on {target:dd MMM}.", $"Happy work anniversary — {years} year{(years == 1 ? "" : "s")}! Thank you.",
                        $"/org/employees/details/{e.Id}", "/me/people"));
                }
                break;
            case ReminderEvent.Birthday:
                foreach (var e in (await Current.Where(e => e.DateOfBirth != null && e.DateOfBirth.Value.Month == target.Month).ToListAsync(ct)).Where(e => SameDay(e.DateOfBirth!.Value, target)))
                    // People who hide their birthday get only their own greeting.
                    list.Add(new($"birthday:{e.Id}:{target.Year}", e.UserId, e.HideBirthday ? null : await ManagerUserAsync(e.ReportingManagerId, ct),
                        e.HideBirthday ? "" : $"{e.FullName}'s birthday is on {target:dd MMM}.", "Happy birthday from all of us!", "/me/people", "/me"));
                break;
            case ReminderEvent.OnboardingTaskOverdue:
                var due = today.AddDays(-rule.Days);
                var tasks = await db.OnboardingTasks.AsNoTracking().Where(t => t.CompletedAtUtc == null && t.DueOn == due && t.Plan.Status == OnboardingStatus.InProgress)
                    .Join(db.Employees, t => t.Plan.EmployeeId, e => e.Id, (t, e) => new { t.Id, t.Title, t.PlanId, e }).ToListAsync(ct);
                foreach (var t in tasks)
                    list.Add(new($"onboarding:{t.Id}", t.e.UserId, await ManagerUserAsync(t.e.ReportingManagerId, ct),
                        $"Onboarding task overdue for {t.e.FullName}: {t.Title}.", $"Your onboarding task is overdue: {t.Title}.", $"/org/onboarding/{t.PlanId}", "/me/onboarding"));
                break;
        }
        return list;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var rules = await db.NotificationRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        if (rules.Count == 0) return 0;
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var sent = 0;
        foreach (var rule in rules)
        {
            var subjects = await SubjectsAsync(rule, today, ct);
            if (subjects.Count == 0) continue;
            var keys = subjects.Select(s => s.Key).ToList();
            var done = (await db.NotificationRuleLogs.Where(l => l.RuleId == rule.Id && keys.Contains(l.SubjectKey)).Select(l => l.SubjectKey).ToListAsync(ct)).ToHashSet();
            var hr = rule.NotifyHr
                ? await notifications.UsersWithPermissionAsync(rule.Event == ReminderEvent.OnboardingTaskOverdue ? TenantPermissions.OnboardingManage : TenantPermissions.EmployeesManage, ct)
                : [];
            foreach (var s in subjects.Where(s => !done.Contains(s.Key)))
            {
                db.NotificationRuleLogs.Add(new NotificationRuleLog { RuleId = rule.Id, SubjectKey = s.Key, SentOn = today });
                if (rule.NotifyEmployee && s.EmployeeUserId is int eu) notifications.Queue([eu], rule.Name, s.EmployeeText, s.EmployeeLink);
                if (s.HrText.Length > 0)
                {
                    var others = new List<int>();
                    if (rule.NotifyManager && s.ManagerUserId is int mu) others.Add(mu);
                    if (rule.NotifyHr) others.AddRange(hr);
                    // Never tell people about their own reminder as "someone else".
                    others.RemoveAll(u => u == s.EmployeeUserId);
                    if (others.Count > 0) notifications.Queue(others, rule.Name, s.HrText, s.HrLink);
                }
                sent++;
            }
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return 0; } // another run sent them at the same moment
        return sent;
    }
}

// ====================================================================== scheduled reports

public sealed class ReportScheduleInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    public ReportKind Kind { get; set; } = ReportKind.JoinersAndExits;
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Monthly;
    [Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    public List<int> RecipientUserIds { get; set; } = [];
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed record ReportScheduleRow(int Id, string Name, ReportKind Kind, ReportFrequency Frequency, string? Department, string? Branch, int Recipients,
    DateOnly NextRunOn, DateTime? LastSentAtUtc, bool IsActive);

public interface IReportScheduleService
{
    Task<IReadOnlyList<ReportScheduleRow>> ListAsync(CancellationToken ct = default);
    Task<ReportScheduleInput?> GetAsync(int id, CancellationToken ct = default);
    /// <summary>Users who may receive reports (they can view HR reports).</summary>
    Task<IReadOnlyList<LookupItem>> RecipientsAsync(CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, ReportScheduleInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SendNowAsync(int id, string? publicUrl, CancellationToken ct = default);
    /// <summary>Sends every schedule that is due today or earlier. Returns emails sent.</summary>
    Task<int> RunDueAsync(string? publicUrl, CancellationToken ct = default);
}

public sealed class ReportScheduleService(IAppDbContext db, ICurrentContext context, IClock clock, IHrReportService reports, IEmailSender email,
    INotificationService notifications) : IReportScheduleService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    /// <summary>Weekly reports go out on Mondays (covering the previous Monday–Sunday); monthly on the 1st (the previous month).</summary>
    public static DateOnly NextRun(ReportFrequency f, DateOnly after) => f switch
    {
        ReportFrequency.Weekly => after.AddDays(((int)DayOfWeek.Monday - (int)after.DayOfWeek + 7) % 7 is 0 ? 7 : ((int)DayOfWeek.Monday - (int)after.DayOfWeek + 7) % 7),
        _ => new DateOnly(after.Year, after.Month, 1).AddMonths(1)
    };

    public static (DateOnly From, DateOnly To) Period(ReportFrequency f, DateOnly runOn) => f switch
    {
        ReportFrequency.Weekly => (runOn.AddDays(-7), runOn.AddDays(-1)),
        _ => (new DateOnly(runOn.Year, runOn.Month, 1).AddMonths(-1), new DateOnly(runOn.Year, runOn.Month, 1).AddDays(-1))
    };

    private static List<int> Ids(string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();

    public async Task<IReadOnlyList<ReportScheduleRow>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.ReportSchedules.AsNoTracking().OrderBy(s => s.Name).Select(s => new
        {
            s.Id, s.Name, s.Kind, s.Frequency, s.RecipientUserIds, s.NextRunOn, s.LastSentAtUtc, s.IsActive,
            Department = db.Departments.Where(d => d.Id == s.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            Branch = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).FirstOrDefault()
        }).ToListAsync(ct);
        return rows.Select(r => new ReportScheduleRow(r.Id, r.Name, r.Kind, r.Frequency, r.Department, r.Branch, Ids(r.RecipientUserIds).Count, r.NextRunOn, r.LastSentAtUtc, r.IsActive)).ToList();
    }

    public async Task<ReportScheduleInput?> GetAsync(int id, CancellationToken ct = default)
    {
        var s = await db.ReportSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new ReportScheduleInput
        {
            Name = s.Name, Kind = s.Kind, Frequency = s.Frequency, DepartmentId = s.DepartmentId, BranchId = s.BranchId, RecipientUserIds = Ids(s.RecipientUserIds), IsActive = s.IsActive
        };
    }

    public async Task<IReadOnlyList<LookupItem>> RecipientsAsync(CancellationToken ct = default)
    {
        var ids = await notifications.UsersWithPermissionAsync(TenantPermissions.ReportsView, ct);
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName).Select(u => new LookupItem(u.Id, u.FullName + " (" + u.Email + ")")).ToListAsync(ct);
    }

    public async Task<Result<int>> SaveAsync(int? id, ReportScheduleInput i, CancellationToken ct = default)
    {
        var allowed = (await notifications.UsersWithPermissionAsync(TenantPermissions.ReportsView, ct)).ToHashSet();
        var recipients = i.RecipientUserIds.Distinct().ToList();
        if (recipients.Count is 0 or > 20) return Result<int>.Fail("Choose between 1 and 20 recipients.");
        if (recipients.Any(r => !allowed.Contains(r))) return Result<int>.Fail("Reports can only be sent to users who may view HR reports.");
        if (i.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Department not found.");
        if (i.BranchId is int b && !await db.Branches.AnyAsync(x => x.Id == b, ct)) return Result<int>.Fail("Branch not found.");
        var s = id is null ? db.ReportSchedules.Add(new ReportSchedule()).Entity : await db.ReportSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<int>.Fail("Schedule not found.");
        if (id is null || s.Frequency != i.Frequency) s.NextRunOn = NextRun(i.Frequency, await TodayAsync(ct));
        s.Name = i.Name.Trim(); s.Kind = i.Kind; s.Frequency = i.Frequency; s.DepartmentId = i.DepartmentId; s.BranchId = i.BranchId;
        s.RecipientUserIds = string.Join(',', recipients); s.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(s.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var s = await db.ReportSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Schedule not found.");
        db.ReportSchedules.Remove(s);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> SendNowAsync(int id, string? publicUrl, CancellationToken ct = default)
    {
        var s = await db.ReportSchedules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<int>.Fail("Schedule not found.");
        var sent = await SendAsync(s, await TodayAsync(ct), publicUrl, ct);
        await db.SaveChangesAsync(ct);
        return sent == 0 ? Result<int>.Fail("Nothing was sent: check the recipients and the email settings.") : Result<int>.Ok(sent);
    }

    public async Task<int> RunDueAsync(string? publicUrl, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var due = await db.ReportSchedules.Where(s => s.IsActive && s.NextRunOn <= today).ToListAsync(ct);
        var sent = 0;
        foreach (var s in due)
        {
            sent += await SendAsync(s, s.NextRunOn, publicUrl, ct);
            s.NextRunOn = NextRun(s.Frequency, today);
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return sent;
    }

    private async Task<int> SendAsync(ReportSchedule s, DateOnly runOn, string? publicUrl, CancellationToken ct)
    {
        var (from, to) = Period(s.Frequency, runOn);
        var filter = new ReportFilter(s.DepartmentId, s.BranchId);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == context.OrgId()).Select(o => o.Name).FirstAsync(ct);
        var scope = string.Join(" · ", new[]
        {
            s.DepartmentId is int d ? await db.Departments.Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync(ct) : null,
            s.BranchId is int b ? await db.Branches.Where(x => x.Id == b).Select(x => x.Name).FirstOrDefaultAsync(ct) : null
        }.Where(x => x is not null));
        var headcount = await reports.HeadcountAsync(filter, ct);
        var joiners = await reports.JoinersAsync(from, to, filter, ct);
        var exits = await reports.ExitsAsync(from, to, filter, ct);

        string E(string? x) => WebUtility.HtmlEncode(x ?? "");
        var html = new StringBuilder();
        html.Append($"<h2 style=\"font-size:18px\">{E(s.Name)}</h2><p>{E(org)}{(scope.Length > 0 ? " · " + E(scope) : "")} · {from:dd MMM yyyy} – {to:dd MMM yyyy}</p>");
        html.Append($"<table cellpadding=\"6\" style=\"border-collapse:collapse\"><tr><td>Current headcount</td><td><strong>{headcount}</strong></td></tr>" +
                    $"<tr><td>Joined in the period</td><td><strong>{joiners.Count}</strong></td></tr><tr><td>Left in the period</td><td><strong>{exits.Count}</strong></td></tr></table>");
        if (s.Kind == ReportKind.JoinersAndExits)
        {
            if (joiners.Count > 0) html.Append("<p><strong>Joiners</strong></p><ul>").Append(string.Concat(joiners.Take(25).Select(j => $"<li>{E(j.Name)} ({E(j.Code)}) · {E(j.Department)} · {j.DateOfJoining:dd MMM}</li>"))).Append(joiners.Count > 25 ? $"<li>and {joiners.Count - 25} more</li>" : "").Append("</ul>");
            if (exits.Count > 0) html.Append("<p><strong>Exits</strong></p><ul>").Append(string.Concat(exits.Take(25).Select(x => $"<li>{E(x.Employee.Name)} ({E(x.Employee.Code)}) · {x.ExitDate:dd MMM}</li>"))).Append(exits.Count > 25 ? $"<li>and {exits.Count - 25} more</li>" : "").Append("</ul>");
        }
        if (!string.IsNullOrWhiteSpace(publicUrl))
            html.Append($"<p><a href=\"{E(publicUrl.TrimEnd('/'))}/org/reports?from={from:yyyy-MM-dd}&amp;to={to:yyyy-MM-dd}&amp;departmentId={s.DepartmentId}&amp;branchId={s.BranchId}\">Open the full report in HRMS</a> (sign-in required).</p>");
        html.Append("<p style=\"color:#888;font-size:12px\">You receive this because you were added to a scheduled report. Ask your HR admin to remove you.</p>");

        // Recipients must still be active and allowed to view reports when the email goes out.
        var allowed = (await notifications.UsersWithPermissionAsync(TenantPermissions.ReportsView, ct)).ToHashSet();
        var ids = Ids(s.RecipientUserIds).Where(allowed.Contains).ToList();
        var mails = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => u.Email).ToListAsync(ct);
        var sent = 0;
        foreach (var m in mails)
            if (await email.SendAsync(new EmailMessage(m, $"{s.Name} · {from:dd MMM} – {to:dd MMM yyyy}", html.ToString(), "report", context.OrganizationId), ct)) sent++;
        if (sent > 0) s.LastSentAtUtc = clock.UtcNow;
        return sent;
    }
}
