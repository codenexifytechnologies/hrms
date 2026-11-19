using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Continuous feedback and one-on-ones. Clock: 15 June 2026 (Monday). Maya manages Arjun and Bela; Chen has no manager.</summary>
public sealed class FeedbackAndOneOnOneTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin;
    private int _maya, _arjun, _bela, _chen;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("talk", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@talk.test").Select(u => u.Id).SingleAsync());
        _maya = await HireAsync("Maya", null);
        _arjun = await HireAsync("Arjun", _maya);
        _bela = await HireAsync("Bela", _maya);
        _chen = await HireAsync("Chen", null);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? manager)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "T", WorkEmail = $"{name.ToLowerInvariant()}@talk.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = manager },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        return r.Value;
    }

    private Task<T> F<T>(Func<IFeedbackService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IFeedbackService>(sp))); }
    private Task<T> M<T>(Func<IOneOnOneService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IOneOnOneService>(sp))); }

    // ------------------------------------------------------------------ feedback

    [Fact]
    public async Task Feedback_visibility_respects_the_writers_choice()
    {
        FeedbackInput Note(FeedbackVisibility v, string body) => new() { AboutEmployeeId = _arjun, Tone = FeedbackTone.Appreciation, Visibility = v, Body = body };
        Assert.True((await F(s => s.GiveAsync(_bela, Note(FeedbackVisibility.Private, "Thanks for covering my demo")))).Succeeded);
        Assert.True((await F(s => s.GiveAsync(_bela, Note(FeedbackVisibility.PersonAndManagers, "Great release notes this week")))).Succeeded);
        Assert.True((await F(s => s.GiveAsync(_chen, Note(FeedbackVisibility.ManagersOnly, "Could be more responsive on tickets")))).Succeeded);
        Assert.False((await F(s => s.GiveAsync(_arjun, Note(FeedbackVisibility.Private, "Feedback for myself")))).Succeeded);
        Assert.False((await F(s => s.GiveAsync(_bela, Note(FeedbackVisibility.Private, "short")))).Succeeded);

        var arjunSees = await F(s => s.ReceivedAsync(_arjun));
        Assert.Equal(2, arjunSees.Count);
        Assert.DoesNotContain(arjunSees, f => f.Visibility == FeedbackVisibility.ManagersOnly);

        var mayaSees = await F(s => s.AboutMyTeamAsync(_maya));
        Assert.Equal(2, mayaSees.Count);
        Assert.DoesNotContain(mayaSees, f => f.Visibility == FeedbackVisibility.Private);
        Assert.Empty(await F(s => s.AboutMyTeamAsync(_bela)));     // not a manager
        Assert.Equal(2, (await F(s => s.GivenAsync(_bela))).Count);
    }

    [Fact]
    public async Task Feedback_requests_are_answered_or_declined_once()
    {
        // Arjun asks two colleagues about himself; Maya asks Chen about Bela; Bela can't ask about a peer.
        var mine = await F(s => s.RequestAsync(_arjun, new FeedbackRequestInput { AboutEmployeeId = _arjun, RecipientIds = [_bela, _chen], Question = "How did the launch go from your side?" }));
        Assert.True(mine.Succeeded, mine.Error);
        Assert.True((await F(s => s.RequestAsync(_maya, new FeedbackRequestInput { AboutEmployeeId = _bela, RecipientIds = [_chen] }))).Succeeded);
        Assert.False((await F(s => s.RequestAsync(_bela, new FeedbackRequestInput { AboutEmployeeId = _arjun, RecipientIds = [_chen] }))).Succeeded);
        Assert.False((await F(s => s.RequestAsync(_arjun, new FeedbackRequestInput { AboutEmployeeId = _arjun, RecipientIds = [_arjun] }))).Succeeded);

        var chenInbox = await F(s => s.ToAnswerAsync(_chen));
        Assert.Equal(2, chenInbox.Count);
        var aboutArjun = chenInbox.Single(a => a.AboutId == _arjun);

        // Answer must be about the person in the request.
        Assert.False((await F(s => s.GiveAsync(_chen, new FeedbackInput { AboutEmployeeId = _bela, RequestId = aboutArjun.RequestId, Body = "Wrong person entirely here" }))).Succeeded);
        Assert.True((await F(s => s.GiveAsync(_chen, new FeedbackInput { AboutEmployeeId = _arjun, RequestId = aboutArjun.RequestId, Tone = FeedbackTone.Suggestion, Body = "Share the plan a day earlier next time." }))).Succeeded);
        Assert.False((await F(s => s.GiveAsync(_chen, new FeedbackInput { AboutEmployeeId = _arjun, RequestId = aboutArjun.RequestId, Body = "Answering a second time" }))).Succeeded);

        var belaRow = (await F(s => s.ToAnswerAsync(_bela))).Single();
        Assert.True((await F(s => s.DeclineAsync(belaRow.RecipientRowId, _bela))).Succeeded);
        Assert.False((await F(s => s.DeclineAsync(belaRow.RecipientRowId, _bela))).Succeeded);

        var status = (await F(s => s.MyRequestsAsync(_arjun))).Single();
        Assert.Contains(status.Recipients, r => r.Status == FeedbackRequestStatus.Answered);
        Assert.Contains(status.Recipients, r => r.Status == FeedbackRequestStatus.Declined);
        Assert.Contains(await F(s => s.ReceivedAsync(_arjun)), f => f.Question == "How did the launch go from your side?");
    }

    [Fact]
    public async Task Review_shows_feedback_from_the_period_with_the_right_visibility()
    {
        Assert.True((await F(s => s.GiveAsync(_bela, new FeedbackInput { AboutEmployeeId = _arjun, Visibility = FeedbackVisibility.Private, Body = "Private thanks for the help" }))).Succeeded);
        Assert.True((await F(s => s.GiveAsync(_chen, new FeedbackInput { AboutEmployeeId = _arjun, Visibility = FeedbackVisibility.ManagersOnly, Body = "A note for the manager only" }))).Succeeded);

        var cycle = await _host.RunAsync(sp => { _host.Context.AsTenant(_org, _admin); return S<IReviewService>(sp).SaveCycleAsync(null, new ReviewCycleInput
        {
            Name = "H1", PeriodStart = new DateOnly(2026, 1, 1), PeriodEnd = new DateOnly(2026, 6, 30), SelfReviewDue = new DateOnly(2026, 6, 25), ManagerReviewDue = new DateOnly(2026, 7, 5)
        }); });
        await _host.RunAsync(sp => S<IReviewService>(sp).LaunchAsync(cycle.Value));
        var reviewId = await _host.RunAsync(sp => S<AppDbContext>(sp).PerformanceReviews.Where(r => r.EmployeeId == _arjun).Select(r => r.Id).SingleAsync());

        var asEmployee = (await _host.RunAsync(sp => S<IReviewService>(sp).GetAsync(reviewId, _arjun, false)))!;
        Assert.Equal(["Private thanks for the help"], asEmployee.Feedback.Select(f => f.Body));
        var asManager = (await _host.RunAsync(sp => S<IReviewService>(sp).GetAsync(reviewId, _maya, false)))!;
        Assert.Equal(["A note for the manager only"], asManager.Feedback.Select(f => f.Body));
    }

    // ------------------------------------------------------------------ one-on-ones

    [Fact]
    public async Task One_on_ones_are_only_within_a_reporting_line()
    {
        var partners = await M(s => s.PartnersAsync(_maya));
        Assert.Equal(new[] { _arjun, _bela }.Order(), partners.Select(p => p.Id).Order());
        Assert.Equal([_maya], (await M(s => s.PartnersAsync(_arjun))).Select(p => p.Id));

        var input = new OneOnOneInput { OtherEmployeeId = _arjun, OnDate = new DateOnly(2026, 6, 15), StartsAt = new TimeOnly(11, 0), Repeat = MeetingRepeat.Weekly, FirstTopic = "Q3 priorities" };
        var byManager = await M(s => s.ScheduleAsync(_maya, input));
        Assert.True(byManager.Succeeded, byManager.Error);
        Assert.False((await M(s => s.ScheduleAsync(_maya, input))).Succeeded);              // same day twice
        Assert.False((await M(s => s.ScheduleAsync(_bela, new OneOnOneInput { OtherEmployeeId = _arjun, OnDate = new DateOnly(2026, 6, 16) }))).Succeeded); // peers
        Assert.False((await M(s => s.ScheduleAsync(_chen, new OneOnOneInput { OtherEmployeeId = _maya, OnDate = new DateOnly(2026, 6, 16) }))).Succeeded);  // unrelated
        Assert.False((await M(s => s.ScheduleAsync(_arjun, new OneOnOneInput { OtherEmployeeId = _maya, OnDate = new DateOnly(2026, 6, 1) }))).Succeeded);  // past

        // An employee can book with their manager; the manager role is worked out either way.
        var byEmployee = await M(s => s.ScheduleAsync(_bela, new OneOnOneInput { OtherEmployeeId = _maya, OnDate = new DateOnly(2026, 6, 17) }));
        Assert.True(byEmployee.Succeeded, byEmployee.Error);
        var view = (await M(s => s.GetAsync(byEmployee.Value, _maya)))!;
        Assert.True(view.IAmManager);
        Assert.Null(await M(s => s.GetAsync(byEmployee.Value, _arjun)));                   // not a participant
        Assert.Equal("Q3 priorities", (await M(s => s.GetAsync(byManager.Value, _arjun)))!.Agenda.Single().Item.Text);
    }

    [Fact]
    public async Task Notes_are_private_where_promised_and_actions_carry_over_to_the_next_meeting()
    {
        var first = (await M(s => s.ScheduleAsync(_maya, new OneOnOneInput { OtherEmployeeId = _arjun, OnDate = new DateOnly(2026, 6, 15), Repeat = MeetingRepeat.Fortnightly }))).Value;
        Assert.True((await M(s => s.SaveNotesAsync(first, _maya, "Discussed the launch", "Consider for promotion"))).Succeeded);
        Assert.True((await M(s => s.SaveNotesAsync(first, _arjun, "Discussed the launch; agreed on dates", "Ask about the conference budget"))).Succeeded);
        Assert.True((await M(s => s.AddItemAsync(first, _maya, MeetingItemKind.Action, "Send conference details", _arjun, new DateOnly(2026, 6, 20)))).Succeeded);
        Assert.False((await M(s => s.AddItemAsync(first, _maya, MeetingItemKind.Action, "Someone else's task", _bela, null))).Succeeded);

        var asMaya = (await M(s => s.GetAsync(first, _maya)))!;
        var asArjun = (await M(s => s.GetAsync(first, _arjun)))!;
        Assert.Equal("Consider for promotion", asMaya.MyPrivateNotes);
        Assert.Equal("Ask about the conference budget", asArjun.MyPrivateNotes);
        Assert.Equal("Discussed the launch; agreed on dates", asMaya.Meeting.SharedNotes);

        // Only the person who added an item can remove it.
        var action = asMaya.Actions.Single().Item.Id;
        Assert.False((await M(s => s.RemoveItemAsync(action, _arjun))).Succeeded);

        // Can't mark a future meeting as held; completing a repeating one schedules the next and carries the open action.
        var future = (await M(s => s.ScheduleAsync(_maya, new OneOnOneInput { OtherEmployeeId = _bela, OnDate = new DateOnly(2026, 6, 30) }))).Value;
        Assert.False((await M(s => s.CompleteAsync(future, _maya))).Succeeded);
        var done = await M(s => s.CompleteAsync(first, _arjun));
        Assert.True(done.Succeeded, done.Error);
        Assert.NotNull(done.Value);
        var next = (await M(s => s.GetAsync(done.Value!.Value, _arjun)))!;
        Assert.Equal(new DateOnly(2026, 6, 29), next.Meeting.OnDate);
        Assert.Equal("Send conference details", next.CarriedOver.Single().Item.Text);
        Assert.Equal(first, next.PreviousId);

        // Ticking it from the new meeting closes it everywhere.
        Assert.True((await M(s => s.ToggleItemAsync(action, _arjun))).Succeeded);
        Assert.Empty((await M(s => s.GetAsync(next.Meeting.Id, _maya)))!.CarriedOver);
        Assert.False((await M(s => s.RemoveItemAsync(action, _maya))).Succeeded); // held meeting keeps its record

        // Cancel and reschedule.
        Assert.True((await M(s => s.RescheduleAsync(next.Meeting.Id, _maya, new DateOnly(2026, 7, 1), new TimeOnly(15, 30)))).Succeeded);
        Assert.True((await M(s => s.CancelAsync(next.Meeting.Id, _arjun))).Succeeded);
        Assert.False((await M(s => s.CancelAsync(next.Meeting.Id, _arjun))).Succeeded);
        Assert.Equal(3, (await M(s => s.MineAsync(_maya))).Count);
    }
}
