using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Core values and skills. Maya manages Arjun and Bela; Chen has no manager. Arjun and Bela are "Developer"s.</summary>
public sealed class ValuesAndSkillsTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin, _developer;
    private int _maya, _arjun, _bela, _chen;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("vals", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@vals.test").Select(u => u.Id).SingleAsync());
        _host.Context.AsTenant(_org, _admin);
        _developer = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var d = new Domain.CoreHr.Designation { OrganizationId = _org, Name = "Developer" };
            db.Designations.Add(d);
            await db.SaveChangesAsync();
            return d.Id;
        });
        _maya = await HireAsync("Maya", null, null);
        _arjun = await HireAsync("Arjun", _maya, _developer);
        _bela = await HireAsync("Bela", _maya, _developer);
        _chen = await HireAsync("Chen", null, null);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? manager, int? designation)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "V", WorkEmail = $"{name.ToLowerInvariant()}@vals.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = manager, DesignationId = designation },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        return r.Value;
    }

    private Task<T> V<T>(Func<ICoreValueService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<ICoreValueService>(sp))); }
    private Task<T> K<T>(Func<ISkillService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<ISkillService>(sp))); }

    // ------------------------------------------------------------------ core values

    [Fact]
    public async Task Values_are_managed_and_shout_outs_are_public_and_limited()
    {
        var customer = await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Customer first", Icon = "heart" }));
        Assert.True(customer.Succeeded, customer.Error);
        Assert.False((await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Customer first", Icon = "heart" }))).Succeeded); // duplicate
        Assert.False((await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Odd icon", Icon = "skull" }))).Succeeded);     // not allowed
        for (var i = 1; i < CoreValueService.MaxActiveValues; i++) Assert.True((await V(s => s.SaveAsync(null, new CoreValueInput { Name = $"Value {i}" }))).Succeeded);
        Assert.Contains("at most", (await V(s => s.SaveAsync(null, new CoreValueInput { Name = "One too many" }))).Error);

        var shout = new ShoutoutInput { ToEmployeeId = _arjun, CoreValueId = customer.Value, Message = "Stayed late to fix the client's outage." };
        Assert.True((await V(s => s.ShoutAsync(_chen, shout))).Succeeded);
        Assert.False((await V(s => s.ShoutAsync(_arjun, shout))).Succeeded); // to self

        // Everyone sees the feed; counts and top list reflect it.
        var feed = await V(s => s.FeedAsync(null, null));
        Assert.Equal("Chen V", feed.Single().FromName);
        Assert.Equal(1, (await V(s => s.ValuesAsync(false))).Single(v => v.Id == customer.Value).RecentShoutouts);
        Assert.Equal(_arjun, (await V(s => s.TopRecognisedAsync(90, 5))).Single().EmployeeId);

        // Only the author or HR can remove one.
        Assert.False((await V(s => s.RemoveAsync(feed[0].Id, _bela, false))).Succeeded);
        Assert.True((await V(s => s.RemoveAsync(feed[0].Id, _chen, false))).Succeeded);

        // Deactivated values can't be used.
        var edit = (await V(s => s.GetForEditAsync(customer.Value)))!;
        edit.IsActive = false;
        Assert.True((await V(s => s.SaveAsync(customer.Value, edit))).Succeeded);
        Assert.False((await V(s => s.ShoutAsync(_chen, shout))).Succeeded);
    }

    [Fact]
    public async Task Daily_shout_out_limit_applies()
    {
        var v = (await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Ownership" }))).Value;
        for (var i = 0; i < CoreValueService.MaxShoutoutsPerDay; i++)
            Assert.True((await V(s => s.ShoutAsync(_chen, new ShoutoutInput { ToEmployeeId = _bela, CoreValueId = v, Message = $"Thanks #{i}" }))).Succeeded);
        Assert.Contains("tomorrow", (await V(s => s.ShoutAsync(_chen, new ShoutoutInput { ToEmployeeId = _bela, CoreValueId = v, Message = "One more" }))).Error);
        _host.Clock.UtcNow = _host.Clock.UtcNow.AddDays(1).AddMinutes(1);
        Assert.True((await V(s => s.ShoutAsync(_chen, new ShoutoutInput { ToEmployeeId = _bela, CoreValueId = v, Message = "Next day" }))).Succeeded);
    }

    [Fact]
    public async Task Review_form_gets_value_questions_and_shows_recognition()
    {
        var v1 = (await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Ownership" }))).Value;
        await V(s => s.SaveAsync(null, new CoreValueInput { Name = "Candour" }));
        Assert.True((await V(s => s.ShoutAsync(_chen, new ShoutoutInput { ToEmployeeId = _arjun, CoreValueId = v1, Message = "Took charge of the release." }))).Succeeded);

        _host.Context.AsTenant(_org, _admin);
        var cycle = (await _host.RunAsync(sp => S<IReviewService>(sp).SaveCycleAsync(null, new ReviewCycleInput
        {
            Name = "H1", PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 6, 30), SelfReviewDue = new DateOnly(2026, 6, 25), ManagerReviewDue = new DateOnly(2026, 7, 5)
        }))).Value;
        Assert.Equal(2, (await _host.RunAsync(sp => S<IReviewService>(sp).AddValueQuestionsAsync(cycle))).Value);
        Assert.Equal(0, (await _host.RunAsync(sp => S<IReviewService>(sp).AddValueQuestionsAsync(cycle))).Value); // no duplicates
        Assert.True((await _host.RunAsync(sp => S<IReviewService>(sp).LaunchAsync(cycle))).Succeeded);

        var reviewId = await _host.RunAsync(sp => S<AppDbContext>(sp).PerformanceReviews.Where(r => r.EmployeeId == _arjun).Select(r => r.Id).SingleAsync());
        var view = (await _host.RunAsync(sp => S<IReviewService>(sp).GetAsync(reviewId, _maya, false)))!;
        Assert.Contains(view.Questions, q => q.Text == ReviewService.ValueQuestion("Ownership"));
        Assert.Equal(1, view.Values.Single(x => x.Value == "Ownership").Count);
    }

    // ------------------------------------------------------------------ skills

    [Fact]
    public async Task Skill_profiles_show_requirements_gaps_and_manager_assessments()
    {
        var csharp = (await K(s => s.SaveAsync(null, new SkillInput { Name = "C#", Category = "Engineering" }))).Value;
        var sql = (await K(s => s.SaveAsync(null, new SkillInput { Name = "SQL", Category = "Engineering" }))).Value;
        Assert.False((await K(s => s.SaveAsync(null, new SkillInput { Name = "C#", Category = "Other" }))).Succeeded);
        Assert.True((await K(s => s.SetRequirementAsync(_developer, csharp, 4))).Succeeded);
        Assert.True((await K(s => s.SetRequirementAsync(_developer, sql, 3))).Succeeded);
        Assert.Equal(2, (await K(s => s.RequirementsAsync(_developer))).Count);

        // Arjun rates himself; required-but-missing skills show as gaps.
        Assert.True((await K(s => s.SetSelfLevelAsync(_arjun, csharp, 3))).Succeeded);
        var mine = (await K(s => s.ProfileAsync(_arjun, _arjun, false)))!;
        Assert.True(mine.IsSelf && !mine.CanAssess);
        Assert.Equal("Developer", mine.Designation);
        Assert.Equal(3, mine.Lines.Single(l => l.Skill == "SQL").Gap);
        Assert.Equal(1, mine.Lines.Single(l => l.Skill == "C#").Gap);

        // Manager's assessment overrides the self-rating; peers and unrelated people can't see or assess.
        Assert.True((await K(s => s.AssessAsync(_maya, false, _arjun, csharp, 4, "Led the API rewrite"))).Succeeded);
        Assert.False((await K(s => s.AssessAsync(_bela, false, _arjun, csharp, 2, null))).Succeeded);
        Assert.False((await K(s => s.AssessAsync(_arjun, false, _arjun, csharp, 5, null))).Succeeded); // self
        Assert.Null(await K(s => s.ProfileAsync(_arjun, _chen, false)));
        Assert.NotNull(await K(s => s.ProfileAsync(_arjun, null, true))); // HR
        var line = (await K(s => s.ProfileAsync(_arjun, _maya, false)))!.Lines.Single(l => l.Skill == "C#");
        Assert.Equal(4, line.Effective);
        Assert.Equal(0, line.Gap);
        Assert.Equal("Led the API rewrite", line.ManagerNote);

        // Removing a requirement removes the gap line for skills nobody has.
        Assert.True((await K(s => s.SetRequirementAsync(_developer, sql, 0))).Succeeded);
        Assert.DoesNotContain((await K(s => s.ProfileAsync(_arjun, _arjun, false)))!.Lines, l => l.Skill == "SQL");
    }

    [Fact]
    public async Task Find_people_by_skill_and_export_the_matrix()
    {
        var python = (await K(s => s.SaveAsync(null, new SkillInput { Name = "Python", Category = "Engineering" }))).Value;
        await K(s => s.SetSelfLevelAsync(_arjun, python, 2));
        await K(s => s.SetSelfLevelAsync(_bela, python, 5));
        await K(s => s.SetSelfLevelAsync(_chen, python, 4));
        await K(s => s.AssessAsync(_maya, false, _bela, python, 3, null)); // manager tempers Bela's 5 to 3

        var found = await K(s => s.FindAsync(python, 3, null));
        Assert.Equal(["Chen V", "Bela V"], found.Select(f => f.EmployeeName));
        Assert.True(found.Single(f => f.EmployeeName == "Bela V").Assessed);

        Assert.True((await K(s => s.SetSelfLevelAsync(_arjun, python, 0))).Succeeded); // remove
        Assert.Equal(2, (await K(s => s.CatalogAsync(false))).Single().People);
        Assert.True((await K(s => s.ExportMatrixAsync(null))).Length > 100);
        Assert.False((await K(s => s.SetSelfLevelAsync(_arjun, python, 7))).Succeeded);
    }
}
