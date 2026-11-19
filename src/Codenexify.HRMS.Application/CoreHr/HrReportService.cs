using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.CoreHr;

public sealed class HeadcountSummary
{
    public int Current { get; init; }
    public int OnProbation { get; init; }
    public int OnNotice { get; init; }
    public int JoinedThisMonth { get; init; }
    public int ExitedThisMonth { get; init; }
    public int PendingExits { get; init; }
    public int DocumentsExpiring30Days { get; init; }
    public IReadOnlyList<LabelValue> ByDepartment { get; init; } = [];
    public IReadOnlyList<LabelValue> ByBranch { get; init; } = [];
    public IReadOnlyList<LabelValue> ByType { get; init; } = [];
    public IReadOnlyList<LabelValue> JoinersByMonth { get; init; } = [];
}

public sealed record ExportFile(byte[] Content, string FileName);

/// <summary>Advanced report filters (Advanced Reports feature). Empty = everyone.</summary>
public sealed record ReportFilter(int? DepartmentId = null, int? BranchId = null, EmploymentType? EmploymentType = null)
{
    public static readonly ReportFilter None = new();
    public bool IsEmpty => DepartmentId is null && BranchId is null && EmploymentType is null;

    public IQueryable<Employee> Apply(IQueryable<Employee> q)
    {
        if (DepartmentId is int d) q = q.Where(e => e.DepartmentId == d);
        if (BranchId is int b) q = q.Where(e => e.BranchId == b);
        if (EmploymentType is { } t) q = q.Where(e => e.EmploymentType == t);
        return q;
    }
}

public interface IHrReportService
{
    Task<HeadcountSummary> HeadcountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeRow>> JoinersAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default);
    Task<IReadOnlyList<(EmployeeRow Employee, DateOnly? ExitDate, ExitType? Type)>> ExitsAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default);
    Task<ExportFile> ExportEmployeesAsync(EmployeeQuery query, CancellationToken ct = default);
    Task<ExportFile> ExportJoinersAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default);
    Task<ExportFile> ExportExitsAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default);
    /// <summary>Current headcount matching the filter.</summary>
    Task<int> HeadcountAsync(ReportFilter filter, CancellationToken ct = default);
}

public sealed class HrReportService(IAppDbContext db, IClock clock, IExcelExporter excel) : IHrReportService
{
    public async Task<HeadcountSummary> HeadcountAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var current = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        var from12 = monthStart.AddMonths(-11);

        var byDept = await current.GroupBy(e => e.Department != null ? e.Department.Name : "Unassigned")
            .Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToListAsync(ct);
        var byBranch = await current.GroupBy(e => e.Branch != null ? e.Branch.Name : "Unassigned")
            .Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToListAsync(ct);
        var byType = await current.GroupBy(e => e.EmploymentType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var joinDates = await db.Employees.AsNoTracking().Where(e => e.DateOfJoining >= from12 && e.DateOfJoining <= today)
            .Select(e => e.DateOfJoining).ToListAsync(ct);
        var expiryLimit = today.AddDays(30);

        return new HeadcountSummary
        {
            Current = await current.CountAsync(ct),
            OnProbation = await current.CountAsync(e => e.Status == EmployeeStatus.Probation, ct),
            OnNotice = await current.CountAsync(e => e.Status == EmployeeStatus.NoticePeriod, ct),
            JoinedThisMonth = await db.Employees.CountAsync(e => e.DateOfJoining >= monthStart && e.DateOfJoining <= today, ct),
            ExitedThisMonth = await db.Employees.CountAsync(e => e.DateOfExit >= monthStart && e.DateOfExit <= today, ct),
            PendingExits = await db.ExitRequests.CountAsync(x => x.Status == ExitStatus.Pending, ct),
            DocumentsExpiring30Days = await db.EmployeeDocuments.CountAsync(d => d.ExpiresOn != null && d.ExpiresOn <= expiryLimit && d.Employee.Status != EmployeeStatus.Exited, ct),
            ByDepartment = byDept.Select(x => new LabelValue(x.Key, x.Count)).ToList(),
            ByBranch = byBranch.Select(x => new LabelValue(x.Key, x.Count)).ToList(),
            ByType = byType.Select(x => new LabelValue(x.Key.ToString(), x.Count)).ToList(),
            JoinersByMonth = Enumerable.Range(0, 12).Select(i => from12.AddMonths(i))
                .Select(m => new LabelValue(m.ToString("MMM yy"), joinDates.Count(d => d.Year == m.Year && d.Month == m.Month))).ToList()
        };
    }

    public Task<int> HeadcountAsync(ReportFilter filter, CancellationToken ct = default) =>
        filter.Apply(db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)).CountAsync(ct);

    public async Task<IReadOnlyList<EmployeeRow>> JoinersAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default) =>
        await EmployeeService.Project((filter ?? ReportFilter.None).Apply(db.Employees.AsNoTracking()).Where(e => e.DateOfJoining >= from && e.DateOfJoining <= to)
            .OrderBy(e => e.DateOfJoining)).ToListAsync(ct);

    public async Task<IReadOnlyList<(EmployeeRow, DateOnly?, ExitType?)>> ExitsAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default)
    {
        var exited = (filter ?? ReportFilter.None).Apply(db.Employees.AsNoTracking()).Where(e => e.DateOfExit >= from && e.DateOfExit <= to);
        var rows = await EmployeeService.Project(exited.OrderBy(e => e.DateOfExit)).ToListAsync(ct);
        var details = await exited.Select(e => new
        {
            e.Id, e.DateOfExit,
            Type = db.ExitRequests.Where(x => x.EmployeeId == e.Id && x.Status == ExitStatus.Completed).OrderByDescending(x => x.Id).Select(x => (ExitType?)x.Type).FirstOrDefault()
        }).ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(r => (r, details[r.Id].DateOfExit, details[r.Id].Type)).ToList();
    }

    private static readonly string[] EmployeeHeaders =
        ["Employee ID", "Name", "Work email", "Department", "Designation", "Branch", "Reporting manager", "Employment type", "Status", "Date of joining"];

    private static object?[] EmployeeCells(EmployeeRow r) =>
        [r.Code, r.Name, r.WorkEmail, r.Department, r.Designation, r.Branch, r.Manager, r.Type.ToString(), r.Status.ToString(), r.DateOfJoining.ToDateTime(TimeOnly.MinValue)];

    public async Task<ExportFile> ExportEmployeesAsync(EmployeeQuery query, CancellationToken ct = default)
    {
        var rows = await EmployeeService.Project(EmployeeService.Filter(db.Employees.AsNoTracking(), query).OrderBy(e => e.EmployeeCode)).ToListAsync(ct);
        return new ExportFile(excel.Export("Employees", EmployeeHeaders, rows.Select(EmployeeCells)), $"employees-{clock.Today:yyyyMMdd}.xlsx");
    }

    public async Task<ExportFile> ExportJoinersAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default)
    {
        var rows = await JoinersAsync(from, to, filter, ct);
        return new ExportFile(excel.Export("Joiners", EmployeeHeaders, rows.Select(EmployeeCells)), $"joiners-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }

    public async Task<ExportFile> ExportExitsAsync(DateOnly from, DateOnly to, ReportFilter? filter, CancellationToken ct = default)
    {
        var rows = await ExitsAsync(from, to, filter, ct);
        var headers = EmployeeHeaders.Concat(["Date of exit", "Exit type"]).ToArray();
        return new ExportFile(excel.Export("Exits", headers,
            rows.Select(r => EmployeeCells(r.Item1).Concat(new object?[] { r.Item2?.ToDateTime(TimeOnly.MinValue), r.Item3?.ToString() }).ToArray())),
            $"exits-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }
}
