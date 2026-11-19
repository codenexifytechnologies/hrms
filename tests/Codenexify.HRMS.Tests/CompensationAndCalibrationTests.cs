using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Compensation cycles and the calibration board. Clock: 15 June 2026. Maya manages Arjun and Bela; Chen has no manager.</summary>
public sealed class CompensationAndCalibrationTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin;
    private int _maya, _arjun, _bela, _chen;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("comp", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@comp.test").Select(u => u.Id).SingleAsync());
        _maya = await HireAsync("Maya", null, 1200000);
        _arjun = await HireAsync("Arjun", _maya, 600000);
        _bela = await HireAsync("Bela", _maya, 500000);
        _chen = await HireAsync("Chen", null, 400000);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? manager, decimal salary)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "C", WorkEmail = $"{name.ToLowerInvariant()}@comp.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = manager },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var structure = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.Select(s => s.Id).FirstAsync());
        Assert.True((await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(r.Value, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = salary, EffectiveFrom = new DateOnly(2025, 1, 1) }))).Succeeded);
        return r.Value;
    }

    private Task<T> C<T>(Func<ICompensationService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<ICompensationService>(sp))); }
    private Task<T> R<T>(Func<IReviewService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IReviewService>(sp))); }

    /// <summary>A review cycle where Arjun is rated 4 and Bela 2 by Maya; Chen and Maya are rated 3 by HR.</summary>
    private async Task<int> RatedReviewCycleAsync()
    {
        var cycle = (await R(s => s.SaveCycleAsync(null, new ReviewCycleInput
        {
            Name = "FY26", PeriodStart = new DateOnly(2025, 4, 1), PeriodEnd = new DateOnly(2026, 3, 31), SelfReviewDue = new DateOnly(2026, 6, 20),
            ManagerReviewDue = new DateOnly(2026, 6, 30), SkipSelfReview = true
        }))).Value;
        Assert.True((await R(s => s.LaunchAsync(cycle))).Succeeded);
        var reviews = await _host.RunAsync(sp => S<AppDbContext>(sp).PerformanceReviews.Where(r => r.ReviewCycleId == cycle).Select(r => new { r.Id, r.EmployeeId, r.ReviewerEmployeeId }).ToListAsync());
        foreach (var r in reviews)
        {
            var view = (await R(s => s.GetAsync(r.Id, r.ReviewerEmployeeId, r.ReviewerEmployeeId is null)))!;
            var answers = view.Questions.Where(q => q.Audience != ReviewAudience.SelfOnly).Select(q => new AnswerInput { QuestionId = q.Id, Rating = q.Type == ReviewQuestionType.Rating ? 3 : null, Text = "ok" }).ToList();
            var rating = r.EmployeeId == _arjun ? 4 : r.EmployeeId == _bela ? 2 : 3;
            var saved = await R(s => s.SaveManagerAsync(r.Id, r.ReviewerEmployeeId, r.ReviewerEmployeeId is null, answers, rating, true));
            Assert.True(saved.Succeeded, saved.Error);
        }
        return cycle;
    }

    private static CompensationCycleInput Cycle(int? reviewCycle) => new()
    {
        Name = "Annual increment 2026", EffectiveOn = new DateOnly(2026, 7, 1), BudgetPercent = 8, ReviewCycleId = reviewCycle,
        Guidelines = [new() { Rating = 5, MinPercent = 12, MaxPercent = 18 }, new() { Rating = 4, MinPercent = 8, MaxPercent = 12 }, new() { Rating = 3, MinPercent = 4, MaxPercent = 8 },
                      new() { Rating = 2, MinPercent = 0, MaxPercent = 3 }, new() { Rating = 1, MinPercent = 0, MaxPercent = 0 }, new() { Rating = 0, MinPercent = 0, MaxPercent = 5 }]
    };

    // ------------------------------------------------------------------ compensation

    [Fact]
    public async Task Merit_cycle_runs_from_planning_to_applied_salaries()
    {
        var reviewCycle = await RatedReviewCycleAsync();
        var id = (await C(s => s.SaveAsync(null, Cycle(reviewCycle)))).Value;
        Assert.Equal(4, (await C(s => s.LaunchAsync(id))).Value);

        // Maya plans only her two reports; ratings come from the review cycle; budget is 8% of their pay.
        var mine = (await C(s => s.ViewAsync(id, _maya)))!;
        Assert.Equal(2, mine.Lines.Count);
        Assert.Equal(4, mine.Lines.Single(l => l.EmployeeId == _arjun).Rating);
        Assert.Equal(88000, mine.Budget); // 8% of 11 lakh
        Assert.Null(await C(s => s.ViewAsync(id, _chen))); // not a planner

        var arjun = mine.Lines.Single(l => l.EmployeeId == _arjun).Id;
        var bela = mine.Lines.Single(l => l.EmployeeId == _bela).Id;
        Assert.True((await C(s => s.SetLineAsync(arjun, _maya, false, 10, null))).Succeeded);
        Assert.Contains("outside the guideline", (await C(s => s.SetLineAsync(bela, _maya, false, 6, null))).Error); // rating 2 allows 0–3%
        Assert.True((await C(s => s.SetLineAsync(bela, _maya, false, 6, "Took over a second product mid-year"))).Succeeded);
        var chenLine = (await C(s => s.ViewAsync(id, null)))!.Lines.Single(l => l.EmployeeId == _chen).Id;
        Assert.False((await C(s => s.SetLineAsync(chenLine, _maya, false, 5, null))).Succeeded); // not hers

        mine = (await C(s => s.ViewAsync(id, _maya)))!;
        Assert.Equal(60000 + 30000, mine.Planned);
        Assert.Equal(1, mine.OutsideGuidelines);

        // Approval needs a decision for everyone; managers are locked out once HR reviews.
        Assert.True((await C(s => s.MoveToReviewAsync(id))).Succeeded);
        Assert.False((await C(s => s.SetLineAsync(arjun, _maya, false, 11, null))).Succeeded);
        Assert.Contains("no decision", (await C(s => s.ApproveAsync(id))).Error);
        var all = (await C(s => s.ViewAsync(id, null)))!;
        foreach (var l in all.Lines.Where(l => l.Percent is null)) Assert.True((await C(s => s.SetLineAsync(l.Id, null, true, l.EmployeeId == _chen ? 5 : 0, l.EmployeeId == _chen ? null : "Raised in January already"))).Succeeded);
        Assert.Equal(0, (await C(s => s.ApproveAsync(id))).Value); // effective 1 July: nothing applied yet

        _host.Clock.UtcNow = new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(3, await C(s => s.ApplyDueAsync()));    // Arjun, Bela, Chen (Maya 0%)
        var newSalary = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).SalaryAsOfAsync(_arjun, new DateOnly(2026, 7, 1)));
        Assert.Equal(660000, newSalary!.AnnualGross);
        Assert.Equal(400000 * 1.05m, (await _host.RunAsync(sp => S<IPayrollSetupService>(sp).SalaryAsOfAsync(_chen, new DateOnly(2026, 7, 1))))!.AnnualGross);
        Assert.Equal(0, await C(s => s.ApplyDueAsync())); // idempotent
        Assert.True((await C(s => s.CloseAsync(id))).Succeeded);
        Assert.NotNull(await C(s => s.ExportAsync(id)));
    }

    [Fact]
    public async Task Cycle_rules_without_ratings()
    {
        var input = Cycle(null);
        input.Guidelines[0].MaxPercent = 1; // max below min
        Assert.Contains(input.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(input)), v => v.ErrorMessage!.Contains("maximum"));

        var id = (await C(s => s.SaveAsync(null, Cycle(null)))).Value;
        Assert.True((await C(s => s.LaunchAsync(id))).Succeeded);
        Assert.False((await C(s => s.LaunchAsync(id))).Succeeded);
        Assert.False((await C(s => s.DeleteAsync(id))).Succeeded);
        var edit = (await C(s => s.GetForEditAsync(id)))!;
        edit.DepartmentId = 999;
        Assert.False((await C(s => s.SaveAsync(id, edit))).Succeeded);

        // Without ratings the 'no rating' guideline (0–5%) applies.
        var line = (await C(s => s.ViewAsync(id, _maya)))!.Lines.First().Id;
        Assert.False((await C(s => s.SetLineAsync(line, _maya, false, 7, null))).Succeeded);
        Assert.True((await C(s => s.SetLineAsync(line, _maya, false, 4, null))).Succeeded);
        Assert.False((await C(s => s.SetLineAsync(line, _maya, false, 120, null))).Succeeded);
    }

    // ------------------------------------------------------------------ calibration

    [Fact]
    public async Task Calibration_board_groups_ratings_compares_targets_and_moves_people()
    {
        var reviewCycle = await RatedReviewCycleAsync();
        _host.Context.AsTenant(_org, _admin);
        Assert.Contains("add up to", (await _host.RunAsync(sp => S<ICalibrationService>(sp).SetTargetsAsync(reviewCycle, [10, 20, 30, 20, 15]))).Error);
        Assert.True((await _host.RunAsync(sp => S<ICalibrationService>(sp).SetTargetsAsync(reviewCycle, [5, 15, 50, 20, 10]))).Succeeded);

        var board = (await _host.RunAsync(sp => S<ICalibrationService>(sp).BoardAsync(reviewCycle, null, null)))!;
        Assert.Equal(4, board.Total);
        Assert.True(board.CanAdjust);
        var three = board.Columns.Single(c => c.Rating == 3);
        Assert.Equal(2, three.Count);
        Assert.Equal(50, three.TargetPercent);
        Assert.Equal(50, board.Percent(three));

        // Filter by reviewer (Maya's team only).
        var team = (await _host.RunAsync(sp => S<ICalibrationService>(sp).BoardAsync(reviewCycle, null, _maya)))!;
        Assert.Equal(2, team.Total);

        // Moving someone uses the review adjustment (reason required) and shows as adjusted.
        var arjunReview = board.Columns.Single(c => c.Rating == 4).Entries.Single().ReviewId;
        Assert.True((await R(s => s.AdjustAsync(arjunReview, 3, "Calibrated against peers"))).Succeeded);
        board = (await _host.RunAsync(sp => S<ICalibrationService>(sp).BoardAsync(reviewCycle, null, null)))!;
        var moved = board.Columns.Single(c => c.Rating == 3).Entries.Single(e => e.EmployeeId == _arjun);
        Assert.True(moved.Adjusted);
        Assert.Equal(4, moved.ManagerRating);

        // After results are released the board is read-only.
        Assert.True((await R(s => s.ReleaseAsync(reviewCycle))).Succeeded);
        Assert.False((await _host.RunAsync(sp => S<ICalibrationService>(sp).BoardAsync(reviewCycle, null, null)))!.CanAdjust);
    }
}
