using Codenexify.HRMS.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

/// <summary>The organization's manager links, loaded once per request, with helpers to walk up and down the chain.</summary>
public sealed class ReportingLine
{
    private readonly Dictionary<int, int?> _managerOf;

    private ReportingLine(Dictionary<int, int?> managerOf) => _managerOf = managerOf;

    public static async Task<ReportingLine> LoadAsync(IAppDbContext db, CancellationToken ct) =>
        new(await db.Employees.AsNoTracking().Select(e => new { e.Id, e.ReportingManagerId }).ToDictionaryAsync(e => e.Id, e => e.ReportingManagerId, ct));

    public int? ManagerOf(int employeeId) => _managerOf.GetValueOrDefault(employeeId);

    /// <summary>Everyone above the employee (direct manager first). Stops at loops.</summary>
    public IReadOnlyList<int> ManagersOf(int employeeId)
    {
        var result = new List<int>();
        var seen = new HashSet<int> { employeeId };
        for (var m = ManagerOf(employeeId); m is int id && seen.Add(id); m = ManagerOf(id)) result.Add(id);
        return result;
    }

    /// <summary>Everyone below the manager, at any depth.</summary>
    public IReadOnlySet<int> ReportsOf(int managerId)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>([managerId]);
        while (queue.Count > 0)
        {
            var m = queue.Dequeue();
            foreach (var (id, mgr) in _managerOf)
                if (mgr == m && id != managerId && result.Add(id)) queue.Enqueue(id);
        }
        return result;
    }

    public bool IsAbove(int managerId, int employeeId) => ManagersOf(employeeId).Contains(managerId);
}
