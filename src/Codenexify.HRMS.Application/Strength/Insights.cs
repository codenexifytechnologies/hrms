using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

public sealed record TimelineEvent(DateOnly Date, string Icon, string Title, string? Detail);

public sealed record MonthPoint(string Label, int Headcount, int Joiners, int Leavers);
public sealed record PeopleDashboard(int Headcount, int Joiners12M, int Leavers12M, decimal AttritionPercent, decimal? AttendanceRate30D, decimal AverageTenureYears,
    IReadOnlyList<MonthPoint> Trend, IReadOnlyList<(string Label, int Count)> ByDepartment, IReadOnlyList<(string Label, int Count)> ByGender,
    IReadOnlyList<(string Label, int Count)> ByTenure, IReadOnlyList<(string Label, int Count)> ByAge, int UnknownAge);

public interface IInsightsService
{
    /// <param name="includePay">Salary revisions and loans only for users allowed to see pay.</param>
    Task<IReadOnlyList<TimelineEvent>> TimelineAsync(int employeeId, bool includePay, CancellationToken ct = default);
    Task<PeopleDashboard> DashboardAsync(CancellationToken ct = default);
}

public sealed class InsightsService(IAppDbContext db, ICurrentContext context, IClock clock) : IInsightsService
{
    public async Task<IReadOnlyList<TimelineEvent>> TimelineAsync(int employeeId, bool includePay, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return [];
        var clockOrg = await db.OrgClockAsync(context.OrgId(), clock, ct);
        DateOnly D(DateTime utc) => DateOnly.FromDateTime(clockOrg.ToLocal(utc));
        var events = new List<TimelineEvent> { new(e.DateOfJoining, "bi-door-open", "Joined", $"as {e.EmploymentType}") };
        if (e.ConfirmationDate is { } conf) events.Add(new(conf, "bi-patch-check", "Confirmed", null));
        if (e.DateOfExit is { } exit) events.Add(new(exit, "bi-box-arrow-right", "Last working day", null));

        foreach (var x in await db.ExitRequests.AsNoTracking().Where(x => x.EmployeeId == employeeId).ToListAsync(ct))
            events.Add(new(x.RequestedOn, "bi-envelope-open", $"{x.Type} submitted", $"{x.Status} · last day {(x.ApprovedLastWorkingDay ?? x.RequestedLastWorkingDay):dd MMM yyyy}"));
        foreach (var l in await db.LeaveApplications.AsNoTracking().Where(l => l.EmployeeId == employeeId && l.Status == LeaveStatus.Approved)
                     .Select(l => new { l.FromDate, l.ToDate, l.Days, Type = l.LeaveType.Name }).ToListAsync(ct))
            events.Add(new(l.FromDate, "bi-calendar2-check", $"{l.Type}: {l.Days:0.#} day(s)", l.FromDate == l.ToDate ? null : $"until {l.ToDate:dd MMM yyyy}"));
        foreach (var l in await db.GeneratedLetters.AsNoTracking().Where(l => l.EmployeeId == employeeId).ToListAsync(ct))
            events.Add(new(l.IssuedOn, "bi-envelope-paper", $"{l.Kind} letter issued", l.RevokedAtUtc is null ? l.Number : $"{l.Number} · revoked"));
        foreach (var d in await db.EmployeeDocuments.AsNoTracking().Where(d => d.EmployeeId == employeeId && d.Category != DocumentCategory.Letter).Select(d => new { d.CreatedAtUtc, d.Title, d.Category }).ToListAsync(ct))
            events.Add(new(D(d.CreatedAtUtc), "bi-file-earmark-arrow-up", $"Document added: {d.Title}", d.Category.ToString()));
        foreach (var a in await db.AssetAssignments.AsNoTracking().Where(a => a.EmployeeId == employeeId).Select(a => new { a.AssignedOn, a.ReturnedOn, a.Asset.Name, a.Asset.AssetTag }).ToListAsync(ct))
        {
            events.Add(new(a.AssignedOn, "bi-laptop", $"Received {a.Name}", a.AssetTag));
            if (a.ReturnedOn is { } r) events.Add(new(r, "bi-arrow-return-left", $"Returned {a.Name}", a.AssetTag));
        }
        foreach (var p in await db.WallPostRecipients.AsNoTracking().Where(r => r.EmployeeId == employeeId)
                     .Join(db.WallPosts, r => r.PostId, p => p.Id, (r, p) => new { p.CreatedAtUtc, p.Badge, p.AuthorName }).ToListAsync(ct))
            events.Add(new(D(p.CreatedAtUtc), "bi-award", $"Praised: {p.Badge}", $"by {p.AuthorName}"));
        foreach (var c in await db.ProfileChangeRequests.AsNoTracking().Where(c => c.EmployeeId == employeeId && c.Status == ProfileChangeStatus.Approved && c.DecidedAtUtc != null).ToListAsync(ct))
            events.Add(new(D(c.DecidedAtUtc!.Value), "bi-person-check", "Personal details updated", $"approved by {c.DecidedBy}"));
        if (includePay)
        {
            var salaries = await db.EmployeeSalaries.AsNoTracking().Where(s => s.EmployeeId == employeeId).OrderBy(s => s.EffectiveFrom).ToListAsync(ct);
            decimal? previous = null;
            foreach (var s in salaries)
            {
                var change = previous is { } p && p > 0 ? $" ({(s.AnnualGross - p) / p * 100:+0.#;-0.#}%)" : "";
                events.Add(new(s.EffectiveFrom, "bi-graph-up-arrow", previous is null ? "Salary set" : "Salary revised", $"₹{s.AnnualGross:N0} a year{change}"));
                previous = s.AnnualGross;
            }
            foreach (var l in await db.EmployeeLoans.AsNoTracking().Where(l => l.EmployeeId == employeeId).ToListAsync(ct))
                events.Add(new(D(l.CreatedAtUtc), "bi-bank", $"{(l.Type == LoanType.SalaryAdvance ? "Salary advance" : "Loan")} of ₹{l.Amount:N0}", l.Status.ToString()));
        }
        return events.OrderByDescending(x => x.Date).ToList();
    }

    public async Task<PeopleDashboard> DashboardAsync(CancellationToken ct = default)
    {
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var people = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Inactive || e.DateOfExit != null)
            .Select(e => new { e.DateOfJoining, e.DateOfExit, e.Status, e.Gender, e.DateOfBirth, Department = e.Department != null ? e.Department.Name : null })
            .ToListAsync(ct);
        bool ActiveOn(DateOnly d, DateOnly joined, DateOnly? left) => joined <= d && (left is null || left >= d);
        var current = people.Where(p => p.Status != EmployeeStatus.Exited && p.Status != EmployeeStatus.Inactive && p.DateOfJoining <= today).ToList();

        var trend = Enumerable.Range(0, 12).Select(i => new DateOnly(today.Year, today.Month, 1).AddMonths(i - 11)).Select(m =>
        {
            var end = m.AddMonths(1).AddDays(-1);
            if (end > today) end = today;
            return new MonthPoint(m.ToString("MMM yy"), people.Count(p => ActiveOn(end, p.DateOfJoining, p.DateOfExit)),
                people.Count(p => p.DateOfJoining >= m && p.DateOfJoining <= end), people.Count(p => p.DateOfExit is { } x && x >= m && x <= end));
        }).ToList();
        var yearAgo = today.AddYears(-1);
        var joiners = people.Count(p => p.DateOfJoining > yearAgo && p.DateOfJoining <= today);
        var leavers = people.Count(p => p.DateOfExit is { } x && x > yearAgo && x <= today);
        var avgHeadcount = trend.Average(t => (decimal)t.Headcount);
        var attrition = avgHeadcount == 0 ? 0 : Math.Round(leavers / avgHeadcount * 100, 1);

        var since = today.AddDays(-30);
        var att = await db.Attendance.AsNoTracking().Where(a => a.Date > since && a.Date <= today)
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        // Working days only: present (half days count half) over present + half-day + absent. Leave, holidays and offs are excluded.
        var present = att.Where(a => a.Key == AttendanceStatus.Present).Sum(a => a.Count) + att.Where(a => a.Key == AttendanceStatus.HalfDay).Sum(a => a.Count) * 0.5m;
        var expected = att.Where(a => a.Key is AttendanceStatus.Present or AttendanceStatus.HalfDay or AttendanceStatus.Absent).Sum(a => a.Count);
        decimal? rate = expected == 0 ? null : Math.Round(present / expected * 100, 1);

        static int Years(DateOnly from, DateOnly to) => to.Year - from.Year - (to.DayOfYear < from.DayOfYear ? 1 : 0);
        var tenure = current.Select(p => (today.DayNumber - p.DateOfJoining.DayNumber) / 365.25m).ToList();
        IReadOnlyList<(string, int)> Buckets(IEnumerable<decimal> values, (string Label, decimal From, decimal To)[] bands) =>
            bands.Select(b => (b.Label, values.Count(v => v >= b.From && v < b.To))).ToList();
        var ages = current.Where(p => p.DateOfBirth is not null).Select(p => (decimal)Years(p.DateOfBirth!.Value, today)).ToList();

        return new PeopleDashboard(current.Count, joiners, leavers, attrition, rate, tenure.Count == 0 ? 0 : Math.Round(tenure.Average(), 1), trend,
            current.GroupBy(p => p.Department ?? "No department").Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList(),
            current.GroupBy(p => p.Gender?.ToString() ?? "Not stated").Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList(),
            Buckets(tenure, [("< 1 year", 0, 1), ("1–3 years", 1, 3), ("3–5 years", 3, 5), ("5+ years", 5, 999)]),
            Buckets(ages, [("Under 25", 0, 25), ("25–34", 25, 35), ("35–44", 35, 45), ("45+", 45, 999)]),
            current.Count - ages.Count);
    }
}
