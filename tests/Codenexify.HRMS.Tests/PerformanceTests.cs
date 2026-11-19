using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Goals & OKRs and performance reviews. Clock: 15 June 2026. Team: Maya manages Arjun and Bela; Chen has no manager.</summary>
public sealed class PerformanceTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org;
    private (int Emp, int User) _maya, _arjun, _bela, _chen;
    private int _admin;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("perf", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@perf.test").Select(u => u.Id).SingleAsync());
        _maya = await HireAsync("Maya", null);
        _arjun = await HireAsync("Arjun", _maya.Emp);
        _bela = await HireAsync("Bela", _maya.Emp);
        _chen = await HireAsync("Chen", null);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<(int, int)> HireAsync(string name, int? manager)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "P", WorkEmail = $"{name.ToLowerInvariant()}@perf.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = manager },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var uid = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync());
        return (r.Value, uid);
    }

    private Task<T> Goals<T>(int user, bool hr, Func<IGoalService, GoalViewer, Task<T>> action)
    {
        _host.Context.AsTenant(_org, user);
        return _host.RunAsync(async sp => { var s = S<IGoalService>(sp); return await action(s, await s.ViewerAsync(hr)); });
    }

    private Task<T> Reviews<T>(int user, Func<IReviewService, Task<T>> action)
    {
        _host.Context.AsTenant(_org, user);
        return _host.RunAsync(sp => action(S<IReviewService>(sp)));
    }

    private static GoalInput Goal(string title, GoalLevel level = GoalLevel.Individual, int? owner = null, int? parent = null) => new()
    {
        Title = title, Level = level, OwnerEmployeeId = owner, ParentGoalId = parent, StartsOn = new DateOnly(2026, 4, 1), DueOn = new DateOnly(2026, 9, 30)
    };

    // ------------------------------------------------------------------ goals

    [Fact]
    public void Key_result_progress_handles_growth_reduction_and_milestones()
    {
        Assert.Equal(50, GoalService.KeyResultProgress(new KeyResult { Type = KeyResultType.Number, StartValue = 0, TargetValue = 200, CurrentValue = 100 }));
        Assert.Equal(75, GoalService.KeyResultProgress(new KeyResult { Type = KeyResultType.Percentage, StartValue = 40, TargetValue = 0, CurrentValue = 10 })); // reduce 40 → 0
        Assert.Equal(100, GoalService.KeyResultProgress(new KeyResult { Type = KeyResultType.Number, StartValue = 0, TargetValue = 10, CurrentValue = 25 }));   // capped
        Assert.Equal(0, GoalService.KeyResultProgress(new KeyResult { Type = KeyResultType.Number, StartValue = 10, TargetValue = 20, CurrentValue = 5 }));    // floored
        Assert.Equal(100, GoalService.KeyResultProgress(new KeyResult { Type = KeyResultType.Milestone, StartValue = 0, TargetValue = 1, CurrentValue = 1 }));
    }

    [Fact]
    public async Task Goal_visibility_and_editing_follow_the_reporting_line()
    {
        var company = await Goals(_admin, true, (s, v) => s.SaveAsync(null, Goal("Grow revenue 30%", GoalLevel.Company), v));
        Assert.True(company.Succeeded, company.Error);
        Assert.False((await Goals(_maya.User, false, (s, v) => s.SaveAsync(null, Goal("Company goal by staff", GoalLevel.Company), v))).Succeeded);

        // Employee sets their own goal, aligned to the company goal; manager sets one for a report; nobody sets one for a peer.
        var own = await Goals(_arjun.User, false, (s, v) => s.SaveAsync(null, Goal("Close 10 deals", parent: company.Value), v));
        Assert.True(own.Succeeded, own.Error);
        Assert.True((await Goals(_maya.User, false, (s, v) => s.SaveAsync(null, Goal("Mentor a new joiner", owner: _bela.Emp), v))).Succeeded);
        Assert.False((await Goals(_bela.User, false, (s, v) => s.SaveAsync(null, Goal("Peer goal", owner: _arjun.Emp), v))).Succeeded);

        // Individual goals: owner, manager and HR can see; a peer and an unrelated employee cannot.
        Assert.NotNull(await Goals(_arjun.User, false, (s, v) => s.GetAsync(own.Value, v)));
        Assert.True((await Goals(_maya.User, false, (s, v) => s.GetAsync(own.Value, v)))!.CanEdit);
        Assert.NotNull(await Goals(_admin, true, (s, v) => s.GetAsync(own.Value, v)));
        Assert.Null(await Goals(_bela.User, false, (s, v) => s.GetAsync(own.Value, v)));
        Assert.Null(await Goals(_chen.User, false, (s, v) => s.GetAsync(own.Value, v)));
        Assert.Empty(await Goals(_chen.User, false, (s, v) => s.IndividualAsync(_arjun.Emp, v)));

        // Company goal is visible to everyone, editable only by HR, and shows aligned goals the viewer may see.
        var seenByMaya = await Goals(_maya.User, false, (s, v) => s.GetAsync(company.Value, v));
        Assert.False(seenByMaya!.CanEdit);
        Assert.Single(seenByMaya.Children);
        Assert.Empty((await Goals(_chen.User, false, (s, v) => s.GetAsync(company.Value, v)))!.Children);

        // Team view for Maya contains both reports' goals.
        var team = await Goals(_maya.User, false, (s, v) => s.TeamAsync(v, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(2, team.Count);

        // Parent rules.
        Assert.False((await Goals(_bela.User, false, (s, v) => s.SaveAsync(null, Goal("Child of individual", parent: own.Value), v))).Succeeded);
        Assert.False((await Goals(_admin, true, (s, v) => s.DeleteAsync(company.Value, v))).Succeeded); // has aligned goals
    }

    [Fact]
    public async Task Check_ins_update_key_results_and_goal_progress()
    {
        var g = (await Goals(_arjun.User, false, (s, v) => s.SaveAsync(null, Goal("Ship v2"), v))).Value;
        Assert.True((await Goals(_arjun.User, false, (s, v) => s.AddKeyResultAsync(g, new KeyResultInput { Title = "Features done", StartValue = 0, TargetValue = 10 }, v))).Succeeded);
        Assert.True((await Goals(_arjun.User, false, (s, v) => s.AddKeyResultAsync(g, new KeyResultInput { Title = "Launch", Type = KeyResultType.Milestone }, v))).Succeeded);
        Assert.False((await Goals(_arjun.User, false, (s, v) => s.AddKeyResultAsync(g, new KeyResultInput { Title = "Bad", StartValue = 5, TargetValue = 5 }, v))).Succeeded);

        var view = (await Goals(_arjun.User, false, (s, v) => s.GetAsync(g, v)))!;
        var features = view.KeyResults.Single(k => k.KeyResult.Title == "Features done").KeyResult.Id;
        Assert.True((await Goals(_arjun.User, false, (s, v) => s.CheckInAsync(g, new CheckInInput { Values = { [features] = 5 }, Status = GoalStatus.AtRisk, Note = "Half way" }, v))).Succeeded);

        view = (await Goals(_maya.User, false, (s, v) => s.GetAsync(g, v)))!;
        Assert.Equal(25, view.Goal.Progress); // (50 + 0) / 2
        Assert.Equal(GoalStatus.AtRisk, view.Goal.Status);
        Assert.Equal("Half way", view.CheckIns.Single().Note);

        // A peer can't check in on someone else's goal.
        Assert.False((await Goals(_bela.User, false, (s, v) => s.CheckInAsync(g, new CheckInInput { Status = GoalStatus.OnTrack }, v))).Succeeded);

        // Goal without key results: progress set directly; Completed means 100%.
        var simple = (await Goals(_arjun.User, false, (s, v) => s.SaveAsync(null, Goal("Read 4 books"), v))).Value;
        Assert.True((await Goals(_arjun.User, false, (s, v) => s.CheckInAsync(simple, new CheckInInput { Progress = 40, Status = GoalStatus.OnTrack }, v))).Succeeded);
        Assert.Equal(40, (await Goals(_arjun.User, false, (s, v) => s.GetAsync(simple, v)))!.Goal.Progress);
        Assert.True((await Goals(_arjun.User, false, (s, v) => s.CheckInAsync(simple, new CheckInInput { Status = GoalStatus.Completed }, v))).Succeeded);
        Assert.Equal(100, (await Goals(_arjun.User, false, (s, v) => s.GetAsync(simple, v)))!.Goal.Progress);
    }

    // ------------------------------------------------------------------ reviews

    private async Task<int> LaunchedCycleAsync()
    {
        var cycle = await Reviews(_admin, s => s.SaveCycleAsync(null, new ReviewCycleInput
        {
            Name = "H1 2026", PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 6, 30),
            SelfReviewDue = new DateOnly(2026, 6, 25), ManagerReviewDue = new DateOnly(2026, 7, 10)
        }));
        Assert.True(cycle.Succeeded, cycle.Error);
        var launched = await Reviews(_admin, s => s.LaunchAsync(cycle.Value));
        Assert.True(launched.Succeeded, launched.Error);
        Assert.Equal(4, launched.Value); // Maya, Arjun, Bela, Chen (the admin login has no employee record)
        return cycle.Value;
    }

    private static List<AnswerInput> Answer(ReviewView v, ReviewRole role, int rating = 4) =>
        v.Questions.Where(q => q.Audience == ReviewAudience.Both || q.Audience == (role == ReviewRole.Self ? ReviewAudience.SelfOnly : ReviewAudience.ManagerOnly))
            .Select(q => new AnswerInput { QuestionId = q.Id, Rating = q.Type == ReviewQuestionType.Rating ? rating : null, Text = q.Type == ReviewQuestionType.Text ? $"{role} answer" : null }).ToList();

    private async Task<int> ReviewIdAsync(int cycle, int employee) =>
        await _host.RunAsync(sp => S<AppDbContext>(sp).PerformanceReviews.IgnoreQueryFilters().Where(r => r.ReviewCycleId == cycle && r.EmployeeId == employee).Select(r => r.Id).SingleAsync());

    [Fact]
    public async Task Review_cycle_runs_from_self_review_to_acknowledgement()
    {
        var cycle = await LaunchedCycleAsync();
        var arjun = await ReviewIdAsync(cycle, _arjun.Emp);
        var chen = await ReviewIdAsync(cycle, _chen.Emp);

        // Reviewer assignment: Maya reviews Arjun; Chen (no manager) is reviewed by HR.
        var mayaQueue = await Reviews(_maya.User, s => s.ToWriteAsync(_maya.Emp, false));
        Assert.Equal(["Arjun P", "Bela P"], mayaQueue.Select(r => r.EmployeeName).Order());
        Assert.Contains(await Reviews(_admin, s => s.ToWriteAsync(null, true)), r => r.Id == chen);

        // Manager can't write before the self-review is in.
        var asMaya = (await Reviews(_maya.User, s => s.GetAsync(arjun, _maya.Emp, false)))!;
        Assert.False(asMaya.CanEditManager);
        Assert.False(asMaya.ShowSelf);
        Assert.Contains("Wait for the self-review", (await Reviews(_maya.User, s => s.SaveManagerAsync(arjun, _maya.Emp, false, [], 4, true))).Error);

        // Self-review: draft, required answers on submit, then locked.
        var asArjun = (await Reviews(_arjun.User, s => s.GetAsync(arjun, _arjun.Emp, false)))!;
        Assert.True(asArjun.CanEditSelf);
        Assert.True((await Reviews(_arjun.User, s => s.SaveSelfAsync(arjun, _arjun.Emp, [], false))).Succeeded); // draft
        Assert.Contains("Give a rating", (await Reviews(_arjun.User, s => s.SaveSelfAsync(arjun, _arjun.Emp, [], true))).Error);
        Assert.True((await Reviews(_arjun.User, s => s.SaveSelfAsync(arjun, _arjun.Emp, Answer(asArjun, ReviewRole.Self), true))).Succeeded);
        Assert.False((await Reviews(_arjun.User, s => s.SaveSelfAsync(arjun, _arjun.Emp, Answer(asArjun, ReviewRole.Self), true))).Succeeded);

        // Others can't open the review; the manager now sees the self-review.
        Assert.Null(await Reviews(_bela.User, s => s.GetAsync(arjun, _bela.Emp, false)));
        asMaya = (await Reviews(_maya.User, s => s.GetAsync(arjun, _maya.Emp, false)))!;
        Assert.True(asMaya.ShowSelf && asMaya.CanEditManager);
        Assert.Contains("overall rating", (await Reviews(_maya.User, s => s.SaveManagerAsync(arjun, _maya.Emp, false, Answer(asMaya, ReviewRole.Manager), null, true))).Error);
        Assert.True((await Reviews(_maya.User, s => s.SaveManagerAsync(arjun, _maya.Emp, false, Answer(asMaya, ReviewRole.Manager), 4, true))).Succeeded);

        // Before release the employee doesn't see the manager's part.
        asArjun = (await Reviews(_arjun.User, s => s.GetAsync(arjun, _arjun.Emp, false)))!;
        Assert.False(asArjun.ShowManager);
        Assert.False(asArjun.CanAcknowledge);

        // HR adjusts (reason required), then releases.
        Assert.False((await Reviews(_admin, s => s.AdjustAsync(arjun, 5, null))).Succeeded);
        Assert.True((await Reviews(_admin, s => s.AdjustAsync(arjun, 5, "Calibrated against the sales team"))).Succeeded);
        var released = await Reviews(_admin, s => s.ReleaseAsync(cycle));
        Assert.True(released.Succeeded, released.Error);
        Assert.Equal(1, released.Value);
        Assert.False((await Reviews(_admin, s => s.AdjustAsync(arjun, 3, "late"))).Succeeded);

        asArjun = (await Reviews(_arjun.User, s => s.GetAsync(arjun, _arjun.Emp, false)))!;
        Assert.True(asArjun.ShowManager && asArjun.CanAcknowledge);
        Assert.Equal(5, asArjun.EffectiveRating);
        Assert.True((await Reviews(_arjun.User, s => s.AcknowledgeAsync(arjun, _arjun.Emp, "Thanks!"))).Succeeded);
        Assert.False((await Reviews(_arjun.User, s => s.AcknowledgeAsync(arjun, _arjun.Emp, null))).Succeeded);

        // A late review in a released cycle goes straight to the employee. HR reviews Chen after his self-review deadline passes.
        _host.Clock.UtcNow = new DateTime(2026, 6, 28, 6, 0, 0, DateTimeKind.Utc);
        var asHr = (await Reviews(_admin, s => s.GetAsync(chen, null, true)))!;
        Assert.True(asHr.CanEditManager); // self-review missed its due date
        Assert.True((await Reviews(_admin, s => s.SaveManagerAsync(chen, null, true, Answer(asHr, ReviewRole.Manager, 3), 3, true))).Succeeded);
        Assert.Equal(ReviewStatus.Released, (await Reviews(_chen.User, s => s.GetAsync(chen, _chen.Emp, false)))!.Review.Status);

        Assert.True((await Reviews(_admin, s => s.CloseAsync(cycle))).Succeeded);
        var summary = (await Reviews(_admin, s => s.CycleAsync(cycle)))!;
        Assert.Equal(1, summary.RatingCounts[5]);
        Assert.Equal(1, summary.RatingCounts[3]);
        Assert.NotNull(await Reviews(_admin, s => s.ExportAsync(cycle)));
    }

    [Fact]
    public async Task Cycle_rules_questions_and_validation()
    {
        var bad = new ReviewCycleInput { Name = "Bad", PeriodStart = new DateOnly(2026, 6, 1), PeriodEnd = new DateOnly(2026, 5, 1), SelfReviewDue = new DateOnly(2026, 7, 1), ManagerReviewDue = new DateOnly(2026, 6, 1), RatingLabels = "A|B|C" };
        var errors = bad.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(bad)).Select(v => v.ErrorMessage).ToList();
        Assert.Equal(3, errors.Count);

        var id = (await Reviews(_admin, s => s.SaveCycleAsync(null, new ReviewCycleInput
        {
            Name = "Probation reviews", PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 6, 30),
            SelfReviewDue = new DateOnly(2026, 7, 1), ManagerReviewDue = new DateOnly(2026, 7, 5), SkipSelfReview = true
        }))).Value;
        var cycle = (await Reviews(_admin, s => s.CycleAsync(id)))!;
        Assert.Equal(6, cycle.Cycle.Questions.Count);
        Assert.Equal(4, cycle.EligibleNow);
        Assert.True((await Reviews(_admin, s => s.AddQuestionAsync(id, new ReviewQuestionInput { Text = "Ready to confirm?", Type = ReviewQuestionType.Text, Audience = ReviewAudience.ManagerOnly }))).Succeeded);

        Assert.True((await Reviews(_admin, s => s.LaunchAsync(id))).Succeeded);
        // Manager-only cycle: reviews start with the manager, questions are frozen, and a second launch is refused.
        var arjun = await ReviewIdAsync(id, _arjun.Emp);
        Assert.True((await Reviews(_maya.User, s => s.GetAsync(arjun, _maya.Emp, false)))!.CanEditManager);
        Assert.False((await Reviews(_admin, s => s.AddQuestionAsync(id, new ReviewQuestionInput { Text = "Late question" }))).Succeeded);
        Assert.False((await Reviews(_admin, s => s.LaunchAsync(id))).Succeeded);
        Assert.False((await Reviews(_admin, s => s.DeleteCycleAsync(id))).Succeeded);
        Assert.False((await Reviews(_admin, s => s.ReleaseAsync(id))).Succeeded); // nothing submitted yet
    }
}
