using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.CoreHr;

public sealed class ExitInitiateInput
{
    public ExitType Type { get; set; } = ExitType.Resignation;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, Display(Name = "Requested last working day")] public DateOnly RequestedLastWorkingDay { get; set; }
}

public sealed record ExitRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string? Department, ExitType Type,
    ExitStatus Status, DateOnly RequestedOn, DateOnly LastWorkingDay, int ChecklistDone, int ChecklistTotal, bool ByEmployee);

public interface IExitService
{
    Task<PagedResult<ExitRow>> ListAsync(ExitStatus? status, int page, CancellationToken ct = default);
    Task<ExitRequest?> GetAsync(int id, CancellationToken ct = default);
    /// <summary>Resignation by the employee (self-service) or exit initiated by HR.</summary>
    Task<Result<int>> InitiateAsync(int employeeId, ExitInitiateInput input, bool byEmployee, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, DateOnly lastWorkingDay, string? notes, CancellationToken ct = default);
    Task<Result> RejectAsync(int id, string notes, CancellationToken ct = default);
    Task<Result> WithdrawAsync(int id, int employeeId, CancellationToken ct = default);
    Task<Result> SetChecklistItemAsync(int id, int itemId, bool completed, CancellationToken ct = default);
    Task<Result> SaveInterviewNotesAsync(int id, string? notes, CancellationToken ct = default);
    /// <summary>Marks the employee as exited and disables their login. Requires a completed checklist.</summary>
    Task<Result> CompleteAsync(int id, CancellationToken ct = default);
    Task<ExitRequest?> LatestForEmployeeAsync(int employeeId, CancellationToken ct = default);
}

public sealed class ExitService(IAppDbContext db, ICurrentContext context, IClock clock, IAuditWriter audit) : IExitService
{
    public async Task<PagedResult<ExitRow>> ListAsync(ExitStatus? status, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.ExitRequests.AsNoTracking();
        if (status is { } s) q = q.Where(x => x.Status == s);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Status == ExitStatus.Pending ? 0 : x.Status == ExitStatus.Approved ? 1 : 2).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new ExitRow(x.Id, x.EmployeeId, x.Employee.FirstName + " " + x.Employee.LastName, x.Employee.EmployeeCode,
                x.Employee.Department != null ? x.Employee.Department.Name : null, x.Type, x.Status, x.RequestedOn,
                x.ApprovedLastWorkingDay ?? x.RequestedLastWorkingDay,
                x.Checklist.Count(c => c.IsCompleted), x.Checklist.Count, x.InitiatedByEmployee))
            .ToListAsync(ct);
        return new PagedResult<ExitRow>(items, total, page, size);
    }

    public Task<ExitRequest?> GetAsync(int id, CancellationToken ct = default) =>
        db.ExitRequests.Include(x => x.Employee).ThenInclude(e => e.Department)
            .Include(x => x.Employee).ThenInclude(e => e.Designation)
            .Include(x => x.Checklist.OrderBy(c => c.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ExitRequest?> LatestForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        db.ExitRequests.AsNoTracking().Include(x => x.Checklist)
            .Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> InitiateAsync(int employeeId, ExitInitiateInput input, bool byEmployee, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return Result<int>.Fail("Employee not found.");
        if (!employee.IsCurrent) return Result<int>.Fail("This employee has already exited.");
        if (await db.ExitRequests.AnyAsync(x => x.EmployeeId == employeeId && (x.Status == ExitStatus.Pending || x.Status == ExitStatus.Approved), ct))
            return Result<int>.Fail("An exit request is already in progress for this employee.");
        if (byEmployee && input.Type != ExitType.Resignation) return Result<int>.Fail("Employees can only submit a resignation.");
        if (input.RequestedLastWorkingDay < clock.Today) return Result<int>.Fail("The last working day cannot be in the past.");

        var exit = new ExitRequest
        {
            EmployeeId = employeeId, Type = input.Type, Reason = input.Reason.Trim(), InitiatedByEmployee = byEmployee,
            RequestedOn = clock.Today, RequestedLastWorkingDay = input.RequestedLastWorkingDay
        };
        db.ExitRequests.Add(exit);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(exit.Id);
    }

    public async Task<Result> ApproveAsync(int id, DateOnly lastWorkingDay, string? notes, CancellationToken ct = default)
    {
        var exit = await GetAsync(id, ct);
        if (exit is null) return Result.Fail("Exit request not found.");
        if (exit.Status != ExitStatus.Pending) return Result.Fail("Only pending requests can be approved.");
        if (lastWorkingDay < exit.RequestedOn) return Result.Fail("Last working day cannot be before the request date.");

        exit.Status = ExitStatus.Approved;
        exit.ApprovedLastWorkingDay = lastWorkingDay;
        exit.DecidedBy = context.UserName;
        exit.DecidedAtUtc = clock.UtcNow;
        exit.DecisionNotes = notes?.Trim();
        exit.Employee.Status = EmployeeStatus.NoticePeriod;

        var template = await db.ExitChecklistTemplateItems.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(ct);
        foreach (var t in template)
            exit.Checklist.Add(new ExitChecklistItem { Title = t.Title, Owner = t.Owner, SortOrder = t.SortOrder });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int id, string notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notes)) return Result.Fail("Please give a reason for rejecting.");
        var exit = await db.ExitRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exit is null) return Result.Fail("Exit request not found.");
        if (exit.Status != ExitStatus.Pending) return Result.Fail("Only pending requests can be rejected.");
        exit.Status = ExitStatus.Rejected;
        exit.DecidedBy = context.UserName;
        exit.DecidedAtUtc = clock.UtcNow;
        exit.DecisionNotes = notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> WithdrawAsync(int id, int employeeId, CancellationToken ct = default)
    {
        var exit = await db.ExitRequests.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (exit is null) return Result.Fail("Exit request not found.");
        if (exit.Status != ExitStatus.Pending) return Result.Fail("Only a pending resignation can be withdrawn. Contact HR.");
        exit.Status = ExitStatus.Withdrawn;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetChecklistItemAsync(int id, int itemId, bool completed, CancellationToken ct = default)
    {
        var item = await db.ExitChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.ExitRequestId == id, ct);
        if (item is null) return Result.Fail("Checklist item not found.");
        if (!await db.ExitRequests.AnyAsync(x => x.Id == id && x.Status == ExitStatus.Approved, ct))
            return Result.Fail("The checklist can only be changed while the exit is approved and open.");
        item.IsCompleted = completed;
        item.CompletedBy = completed ? context.UserName : null;
        item.CompletedAtUtc = completed ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SaveInterviewNotesAsync(int id, string? notes, CancellationToken ct = default)
    {
        var exit = await db.ExitRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exit is null) return Result.Fail("Exit request not found.");
        exit.ExitInterviewNotes = notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CompleteAsync(int id, CancellationToken ct = default)
    {
        var exit = await GetAsync(id, ct);
        if (exit is null) return Result.Fail("Exit request not found.");
        if (exit.Status != ExitStatus.Approved) return Result.Fail("Only approved exits can be completed.");
        var pending = exit.Checklist.Count(c => !c.IsCompleted);
        if (pending > 0) return Result.Fail($"{pending} checklist item(s) are still open.");

        exit.Status = ExitStatus.Completed;
        exit.CompletedAtUtc = clock.UtcNow;
        var employee = exit.Employee;
        employee.Status = EmployeeStatus.Exited;
        employee.DateOfExit = exit.ApprovedLastWorkingDay ?? exit.RequestedLastWorkingDay;

        if (employee.UserId is int uid)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
            if (user is not null && !user.IsOrganizationOwner)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N"); // ends active sessions
            }
        }
        // Reports of the leaving employee move up to their manager.
        foreach (var report in await db.Employees.Where(e => e.ReportingManagerId == employee.Id).ToListAsync(ct))
            report.ReportingManagerId = employee.ReportingManagerId;

        audit.Add("Employee.Exited", nameof(Employee), employee.Id.ToString(), $"{employee.EmployeeCode} exited on {employee.DateOfExit:dd-MMM-yyyy} ({exit.Type})");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
