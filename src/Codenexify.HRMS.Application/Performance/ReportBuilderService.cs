using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public enum ReportFieldType { Text, Number, Date }
public sealed record ReportField(string Key, string Label, ReportFieldType Type);

/// <summary>What a report shows. Every key is checked against the source's field list before use.</summary>
public sealed class ReportDefinition
{
    public List<string> Columns { get; set; } = [];
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    /// <summary>"current" (default), "exited" or "all".</summary>
    public string People { get; set; } = "current";
    /// <summary>Date range: leave overlapping it, attendance/payroll months in it, or joining dates for the employee source.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>Optional: count rows per value of this column (numeric columns are summed).</summary>
    public string? GroupBy { get; set; }
    public string? SortBy { get; set; }
    public bool Descending { get; set; }
}

public sealed class SavedReportInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ReportSource Source { get; set; }
    [Display(Name = "Share with everyone who can build reports")] public bool IsShared { get; set; }
    public ReportDefinition Definition { get; set; } = new();
}

public sealed record ReportResult(IReadOnlyList<ReportField> Columns, IReadOnlyList<object?[]> Rows, IReadOnlyDictionary<string, decimal> Totals, bool Truncated);
public sealed record SavedReportRow(int Id, string Name, ReportSource Source, bool IsShared, bool IsMine, string OwnerName, DateTime? LastRunAtUtc);

public interface IReportBuilderService
{
    IReadOnlyList<ReportField> Fields(ReportSource source);
    /// <param name="payrollAllowed">The viewer may see salaries (payroll source).</param>
    Task<Result<ReportResult>> RunAsync(ReportSource source, ReportDefinition definition, bool payrollAllowed, CancellationToken ct = default);
    Task<IReadOnlyList<SavedReportRow>> SavedAsync(int userId, CancellationToken ct = default);
    Task<(SavedReportInput Input, bool IsMine)?> GetAsync(int id, int userId, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, int userId, SavedReportInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, int userId, CancellationToken ct = default);
    Task<byte[]?> ExportAsync(ReportResult result, string sheetName);
    Task MarkRunAsync(int id, CancellationToken ct = default);
}

public sealed class ReportBuilderService(IAppDbContext db, ICurrentContext context, IClock clock, IExcelExporter excel) : IReportBuilderService
{
    public const int MaxRows = 5000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<ReportSource, IReadOnlyList<ReportField>> Catalog = new Dictionary<ReportSource, IReadOnlyList<ReportField>>
    {
        [ReportSource.Employees] =
        [
            new("code", "Employee code", ReportFieldType.Text), new("name", "Name", ReportFieldType.Text), new("email", "Work email", ReportFieldType.Text),
            new("department", "Department", ReportFieldType.Text), new("designation", "Job title", ReportFieldType.Text), new("branch", "Branch", ReportFieldType.Text),
            new("manager", "Reporting manager", ReportFieldType.Text), new("type", "Employment type", ReportFieldType.Text), new("status", "Status", ReportFieldType.Text),
            new("gender", "Gender", ReportFieldType.Text), new("joined", "Joining date", ReportFieldType.Date), new("exited", "Exit date", ReportFieldType.Date),
            new("tenure", "Tenure (years)", ReportFieldType.Number), new("headcount", "Headcount", ReportFieldType.Number),
        ],
        [ReportSource.Leave] =
        [
            new("code", "Employee code", ReportFieldType.Text), new("name", "Name", ReportFieldType.Text), new("department", "Department", ReportFieldType.Text),
            new("leaveType", "Leave type", ReportFieldType.Text), new("from", "From", ReportFieldType.Date), new("to", "To", ReportFieldType.Date),
            new("days", "Days", ReportFieldType.Number), new("status", "Status", ReportFieldType.Text), new("paid", "Paid leave", ReportFieldType.Text),
        ],
        [ReportSource.Attendance] =
        [
            new("code", "Employee code", ReportFieldType.Text), new("name", "Name", ReportFieldType.Text), new("department", "Department", ReportFieldType.Text),
            new("month", "Month", ReportFieldType.Text), new("present", "Present days", ReportFieldType.Number), new("halfDays", "Half days", ReportFieldType.Number),
            new("absent", "Absent days", ReportFieldType.Number), new("leave", "Leave days", ReportFieldType.Number), new("lateDays", "Late days", ReportFieldType.Number),
            new("lateMinutes", "Late minutes", ReportFieldType.Number), new("overtimeHours", "Overtime hours", ReportFieldType.Number), new("workedHours", "Hours worked", ReportFieldType.Number),
        ],
        [ReportSource.Payroll] =
        [
            new("code", "Employee code", ReportFieldType.Text), new("name", "Name", ReportFieldType.Text), new("department", "Department", ReportFieldType.Text),
            new("month", "Month", ReportFieldType.Text), new("paidDays", "Paid days", ReportFieldType.Number), new("lopDays", "LOP days", ReportFieldType.Number),
            new("gross", "Gross earnings", ReportFieldType.Number), new("deductions", "Deductions", ReportFieldType.Number), new("net", "Net pay", ReportFieldType.Number),
            new("employerCost", "Employer contributions", ReportFieldType.Number),
        ],
    };

    public IReadOnlyList<ReportField> Fields(ReportSource source) => Catalog[source];

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    public async Task<Result<ReportResult>> RunAsync(ReportSource source, ReportDefinition d, bool payrollAllowed, CancellationToken ct = default)
    {
        if (source == ReportSource.Payroll && !payrollAllowed) return Result<ReportResult>.Fail("You need payroll access for payroll reports.");
        var fields = Catalog[source].ToDictionary(f => f.Key);
        var columns = d.Columns.Where(fields.ContainsKey).Distinct().Select(k => fields[k]).ToList();
        if (columns.Count == 0) return Result<ReportResult>.Fail("Choose at least one column.");
        if (d.GroupBy is { Length: > 0 } g && !fields.ContainsKey(g)) return Result<ReportResult>.Fail("Unknown group-by column.");
        if (d.From is { } f && d.To is { } t && t < f) return Result<ReportResult>.Fail("The date range ends before it starts.");

        var today = await TodayAsync(ct);
        var people = db.Employees.AsNoTracking().AsQueryable();
        if (d.DepartmentId is int dep) people = people.Where(e => e.DepartmentId == dep);
        if (d.BranchId is int br) people = people.Where(e => e.BranchId == br);
        if (d.EmploymentType is { } et) people = people.Where(e => e.EmploymentType == et);
        people = d.People switch
        {
            "exited" => people.Where(e => e.Status == EmployeeStatus.Exited || e.Status == EmployeeStatus.Inactive),
            "all" => people,
            _ => people.Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
        };

        var rows = source switch
        {
            ReportSource.Employees => await EmployeesAsync(people, d, today, ct),
            ReportSource.Leave => await LeaveAsync(people, d, today, ct),
            ReportSource.Attendance => await AttendanceAsync(people, d, today, ct),
            _ => await PayrollAsync(people, d, today, ct)
        };

        // Group: one row per value of the group column; numbers summed, a "Rows" count added.
        if (d.GroupBy is { Length: > 0 } groupKey)
        {
            var groupField = fields[groupKey];
            var numeric = columns.Where(c => c.Type == ReportFieldType.Number && c.Key != groupKey).ToList();
            columns = [groupField, .. numeric, new ReportField("_rows", "Rows", ReportFieldType.Number)];
            rows = rows.GroupBy(r => r.GetValueOrDefault(groupKey)?.ToString() ?? "—")
                .Select(gr =>
                {
                    var row = new Dictionary<string, object?> { [groupKey] = gr.Key, ["_rows"] = (decimal)gr.Count() };
                    foreach (var n in numeric) row[n.Key] = gr.Sum(x => Convert.ToDecimal(x.GetValueOrDefault(n.Key) ?? 0m));
                    return row;
                }).ToList();
        }

        var sortKey = d.SortBy is { Length: > 0 } s && columns.Any(c => c.Key == s) ? s : columns[0].Key;
        rows = (d.Descending ? rows.OrderByDescending(r => r.GetValueOrDefault(sortKey), Comparer.Instance) : rows.OrderBy(r => r.GetValueOrDefault(sortKey), Comparer.Instance)).ToList();
        var truncated = rows.Count > MaxRows;
        var output = rows.Take(MaxRows).Select(r => columns.Select(c => r.GetValueOrDefault(c.Key)).ToArray()).ToList();
        var totals = columns.Select((c, i) => (c, i)).Where(x => x.c.Type == ReportFieldType.Number && x.c.Key != "tenure")
            .ToDictionary(x => x.c.Key, x => output.Sum(r => Convert.ToDecimal(r[x.i] ?? 0m)));
        return Result<ReportResult>.Ok(new ReportResult(columns, output, totals, truncated));
    }

    private sealed class Comparer : IComparer<object?>
    {
        public static readonly Comparer Instance = new();
        public int Compare(object? x, object? y) => (x, y) switch
        {
            (null, null) => 0, (null, _) => -1, (_, null) => 1,
            (IComparable a, _) when x.GetType() == y.GetType() => a.CompareTo(y),
            _ => string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string Name(Employee e) => string.Join(' ', new[] { e.FirstName, e.MiddleName, e.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
    private static string Label(EmploymentType t) => t switch { EmploymentType.FullTime => "Full-time", EmploymentType.PartTime => "Part-time", _ => t.ToString() };

    private async Task<List<Dictionary<string, object?>>> EmployeesAsync(IQueryable<Employee> people, ReportDefinition d, DateOnly today, CancellationToken ct)
    {
        IQueryable<Employee> q = people.Include(e => e.Department).Include(e => e.Designation).Include(e => e.Branch).Include(e => e.ReportingManager);
        if (d.From is { } f) q = q.Where(e => e.DateOfJoining >= f);
        if (d.To is { } t) q = q.Where(e => e.DateOfJoining <= t);
        var list = await q.Take(MaxRows + 1).ToListAsync(ct);
        return list.Select(e => new Dictionary<string, object?>
        {
            ["code"] = e.EmployeeCode, ["name"] = Name(e), ["email"] = e.WorkEmail, ["department"] = e.Department?.Name, ["designation"] = e.Designation?.Name,
            ["branch"] = e.Branch?.Name, ["manager"] = e.ReportingManager is { } m ? Name(m) : null, ["type"] = Label(e.EmploymentType), ["status"] = e.Status.ToString(),
            ["gender"] = e.Gender?.ToString(), ["joined"] = e.DateOfJoining, ["exited"] = e.DateOfExit,
            ["tenure"] = Math.Round(((e.DateOfExit ?? today).DayNumber - e.DateOfJoining.DayNumber) / 365.25m, 1), ["headcount"] = 1m
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LeaveAsync(IQueryable<Employee> people, ReportDefinition d, DateOnly today, CancellationToken ct)
    {
        var from = d.From ?? new DateOnly(today.Year, 1, 1);
        var to = d.To ?? new DateOnly(today.Year, 12, 31);
        var ids = people.Select(e => e.Id);
        var list = await db.LeaveApplications.AsNoTracking().Include(l => l.LeaveType).Include(l => l.Employee).ThenInclude(e => e.Department)
            .Where(l => ids.Contains(l.EmployeeId) && l.FromDate <= to && l.ToDate >= from).OrderBy(l => l.FromDate).Take(MaxRows + 1).ToListAsync(ct);
        return list.Select(l => new Dictionary<string, object?>
        {
            ["code"] = l.Employee.EmployeeCode, ["name"] = Name(l.Employee), ["department"] = l.Employee.Department?.Name, ["leaveType"] = l.LeaveType.Name,
            ["from"] = l.FromDate, ["to"] = l.ToDate, ["days"] = l.Days, ["status"] = l.Status.ToString(), ["paid"] = l.LeaveType.IsPaid ? "Yes" : "No"
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> AttendanceAsync(IQueryable<Employee> people, ReportDefinition d, DateOnly today, CancellationToken ct)
    {
        var from = d.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = d.To ?? today;
        if (to.DayNumber - from.DayNumber > 366) from = to.AddDays(-366);
        var ids = people.Select(e => e.Id);
        var days = await db.Attendance.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to)
            .Select(a => new { a.EmployeeId, a.Date, a.Status, a.LateMinutes, a.OvertimeMinutes, a.WorkedMinutes }).ToListAsync(ct);
        var empIds = days.Select(x => x.EmployeeId).Distinct().ToList();
        var emps = await db.Employees.AsNoTracking().Include(e => e.Department).Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return days.GroupBy(x => (x.EmployeeId, Month: $"{x.Date:yyyy-MM}")).Select(g =>
        {
            var e = emps[g.Key.EmployeeId];
            return new Dictionary<string, object?>
            {
                ["code"] = e.EmployeeCode, ["name"] = Name(e), ["department"] = e.Department?.Name, ["month"] = g.Key.Month,
                ["present"] = (decimal)g.Count(x => x.Status == AttendanceStatus.Present), ["halfDays"] = (decimal)g.Count(x => x.Status is AttendanceStatus.HalfDay or AttendanceStatus.HalfDayLeave),
                ["absent"] = (decimal)g.Count(x => x.Status == AttendanceStatus.Absent), ["leave"] = g.Sum(x => x.Status == AttendanceStatus.Leave ? 1m : x.Status == AttendanceStatus.HalfDayLeave ? 0.5m : 0m),
                ["lateDays"] = (decimal)g.Count(x => x.LateMinutes > 0), ["lateMinutes"] = (decimal)g.Sum(x => x.LateMinutes),
                ["overtimeHours"] = Math.Round(g.Sum(x => x.OvertimeMinutes) / 60m, 1), ["workedHours"] = Math.Round(g.Sum(x => x.WorkedMinutes) / 60m, 1)
            };
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> PayrollAsync(IQueryable<Employee> people, ReportDefinition d, DateOnly today, CancellationToken ct)
    {
        var from = d.From ?? new DateOnly(today.Year, 1, 1);
        var to = d.To ?? today;
        int fromKey = from.Year * 100 + from.Month, toKey = to.Year * 100 + to.Month;
        var ids = people.Select(e => e.Id);
        var rows = await db.PayrollDetails.AsNoTracking()
            .Where(x => ids.Contains(x.EmployeeId) && (x.Run.Status == PayrollRunStatus.Finalized || x.Run.Status == PayrollRunStatus.Paid)
                        && x.Run.Year * 100 + x.Run.Month >= fromKey && x.Run.Year * 100 + x.Run.Month <= toKey)
            .Select(x => new { x.EmployeeId, x.Run.Year, x.Run.Month, x.PaidDays, x.LopDays, x.GrossEarnings, x.TotalDeductions, x.NetPay, x.EmployerContributions })
            .Take(MaxRows + 1).ToListAsync(ct);
        var empIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
        var emps = await db.Employees.AsNoTracking().Include(e => e.Department).Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        return rows.Select(r => new Dictionary<string, object?>
        {
            ["code"] = emps[r.EmployeeId].EmployeeCode, ["name"] = Name(emps[r.EmployeeId]), ["department"] = emps[r.EmployeeId].Department?.Name,
            ["month"] = $"{r.Year}-{r.Month:00}", ["paidDays"] = r.PaidDays, ["lopDays"] = r.LopDays, ["gross"] = r.GrossEarnings, ["deductions"] = r.TotalDeductions,
            ["net"] = r.NetPay, ["employerCost"] = r.EmployerContributions
        }).ToList();
    }

    // ------------------------------------------------------------------ saved reports

    public async Task<IReadOnlyList<SavedReportRow>> SavedAsync(int userId, CancellationToken ct = default) =>
        await (from r in db.SavedReports.AsNoTracking().Where(r => r.OwnerUserId == userId || r.IsShared)
               join u in db.Users on r.OwnerUserId equals u.Id
               orderby r.Name
               select new SavedReportRow(r.Id, r.Name, r.Source, r.IsShared, r.OwnerUserId == userId, u.FullName, r.LastRunAtUtc)).ToListAsync(ct);

    public async Task<(SavedReportInput Input, bool IsMine)?> GetAsync(int id, int userId, CancellationToken ct = default)
    {
        var r = await db.SavedReports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && (x.OwnerUserId == userId || x.IsShared), ct);
        if (r is null) return null;
        var def = JsonSerializer.Deserialize<ReportDefinition>(r.Definition, Json) ?? new ReportDefinition();
        return (new SavedReportInput { Name = r.Name, Source = r.Source, IsShared = r.IsShared, Definition = def }, r.OwnerUserId == userId);
    }

    public async Task<Result<int>> SaveAsync(int? id, int userId, SavedReportInput i, CancellationToken ct = default)
    {
        var fields = Catalog[i.Source].Select(f => f.Key).ToHashSet();
        i.Definition.Columns = i.Definition.Columns.Where(fields.Contains).Distinct().ToList();
        if (i.Definition.Columns.Count == 0) return Result<int>.Fail("Choose at least one column.");
        if (i.Definition.GroupBy is { Length: > 0 } g && !fields.Contains(g)) i.Definition.GroupBy = null;
        SavedReport r;
        if (id is int existing)
        {
            r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == existing && x.OwnerUserId == userId, ct) ?? null!;
            if (r is null) return Result<int>.Fail("Only the owner can change a saved report. Save it under a new name instead.");
        }
        else
        {
            if (await db.SavedReports.CountAsync(x => x.OwnerUserId == userId, ct) >= 50) return Result<int>.Fail("You can save up to 50 reports.");
            r = new SavedReport { OwnerUserId = userId };
            db.SavedReports.Add(r);
        }
        r.Name = i.Name.Trim();
        r.Source = i.Source;
        r.IsShared = i.IsShared;
        r.Definition = JsonSerializer.Serialize(i.Definition, Json);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(r.Id);
    }

    public async Task<Result> DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct);
        if (r is null) return Result.Fail("Only the owner can delete a saved report.");
        db.SavedReports.Remove(r);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<byte[]?> ExportAsync(ReportResult result, string sheetName) =>
        Task.FromResult<byte[]?>(excel.Export(sheetName.Length > 31 ? sheetName[..31] : sheetName, result.Columns.Select(c => c.Label).ToList(),
            result.Rows.Select(r => (IReadOnlyList<object?>)r.Select(v => v is DateOnly dt ? dt.ToString("dd-MM-yyyy") : v).ToList())));

    public async Task MarkRunAsync(int id, CancellationToken ct = default)
    {
        var r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        r.LastRunAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
