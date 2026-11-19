using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Engagement;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class EngagementTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other, _admin, _otherAdmin;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "GROWTH");
        _other = await _host.ProvisionAsync("globex", "GROWTH");
        _admin = await OwnerAsync(_org);
        _otherAdmin = await OwnerAsync(_other);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private Task<int> OwnerAsync(int org)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => S<AppDbContext>(sp).Users.IgnoreQueryFilters().Where(u => u.OrganizationId == org && u.IsOrganizationOwner).Select(u => u.Id).SingleAsync());
    }

    private async Task<(int EmployeeId, int UserId)> EmployeeAsync(int org, string name, DateOnly? dob = null, DateOnly? joined = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "T", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = joined ?? new DateOnly(2024, 1, 1) },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        return (r.Value, await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var e = await db.Employees.SingleAsync(x => x.Id == r.Value);
            e.DateOfBirth = dob;
            e.MobilePhone = "9800000000";
            await db.SaveChangesAsync();
            return e.UserId!.Value;
        }));
    }

    private Task<T> Wall<T>(int org, int user, Func<ISocialWallService, Task<T>> a)
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => a(S<ISocialWallService>(sp)));
    }

    private Task<int> UnreadAsync(int org, int user)
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => S<Application.Time.INotificationService>(sp).UnreadCountAsync());
    }

    // ------------------------------------------------------------------ wall

    [Fact]
    public async Task Posts_likes_and_comments_stay_inside_the_organization()
    {
        var (_, asha) = await EmployeeAsync(_org, "Asha");
        var (_, ravi) = await EmployeeAsync(_org, "Ravi");
        var post = (await Wall(_org, asha, w => w.PostAsync(new PostInput { Body = "Welcome to the new office!" }))).Value;

        Assert.True((await Wall(_org, ravi, w => w.ToggleLikeAsync(post))).Value);
        Assert.False((await Wall(_org, ravi, w => w.ToggleLikeAsync(post))).Value); // second click un-likes
        Assert.True((await Wall(_org, ravi, w => w.ToggleLikeAsync(post))).Value);
        Assert.True((await Wall(_org, ravi, w => w.CommentAsync(post, "Looks great"))).Succeeded);
        Assert.False((await Wall(_org, ravi, w => w.CommentAsync(post, "   "))).Succeeded);

        var view = (await Wall(_org, asha, w => w.FeedAsync(1, null, false))).Items.Single(p => p.Id == post);
        Assert.Equal((1, 1, false, true), (view.LikeCount, view.CommentCount, view.LikedByMe, view.CanDelete));
        Assert.Equal(1, await UnreadAsync(_org, asha)); // Ravi's comment

        // Another organization sees nothing and cannot interact.
        Assert.Empty((await Wall(_other, _otherAdmin, w => w.FeedAsync(1, null, false))).Items);
        Assert.False((await Wall(_other, _otherAdmin, w => w.ToggleLikeAsync(post))).Succeeded);
        Assert.False((await Wall(_other, _otherAdmin, w => w.CommentAsync(post, "hi"))).Succeeded);
        Assert.False((await Wall(_other, _otherAdmin, w => w.DeleteAsync(post, canModerate: true))).Succeeded);

        // Only the author or a moderator may remove it.
        Assert.False((await Wall(_org, ravi, w => w.DeleteAsync(post, canModerate: false))).Succeeded);
        Assert.True((await Wall(_org, _admin, w => w.DeleteAsync(post, canModerate: true))).Succeeded);
        Assert.Empty((await Wall(_org, asha, w => w.FeedAsync(1, null, false))).Items);
    }

    [Fact]
    public async Task Praise_notifies_colleagues_and_cannot_target_yourself()
    {
        var (ashaEmp, asha) = await EmployeeAsync(_org, "Asha");
        var (raviEmp, ravi) = await EmployeeAsync(_org, "Ravi");
        var (otherEmp, _) = await EmployeeAsync(_other, "Zed");

        Assert.False((await Wall(_org, asha, w => w.PraiseAsync(new PraiseInput { EmployeeIds = [ashaEmp], Badge = "Team player", Body = "Me, myself and I" }))).Succeeded);
        Assert.False((await Wall(_org, asha, w => w.PraiseAsync(new PraiseInput { EmployeeIds = [otherEmp], Badge = "Team player", Body = "Not my colleague" }))).Succeeded);
        Assert.False((await Wall(_org, asha, w => w.PraiseAsync(new PraiseInput { EmployeeIds = [raviEmp], Badge = "Made up", Body = "Unknown badge" }))).Succeeded);

        var id = await Wall(_org, asha, w => w.PraiseAsync(new PraiseInput { EmployeeIds = [raviEmp], Badge = "Problem solver", Body = "Fixed the payroll export in an hour!" }));
        Assert.True(id.Succeeded, id.Error);
        var view = await Wall(_org, ravi, w => w.GetAsync(id.Value, false, true));
        Assert.Equal(("Problem solver", "Ravi T"), (view!.Badge, view.Recipients.Single().Name));
        Assert.Equal(1, await UnreadAsync(_org, ravi));
    }

    [Fact]
    public async Task Polls_allow_one_anonymous_vote_and_hide_results_until_you_vote()
    {
        var (_, asha) = await EmployeeAsync(_org, "Asha");
        var (_, ravi) = await EmployeeAsync(_org, "Ravi");
        Assert.False((await Wall(_org, asha, w => w.PollAsync(new PollInput { Question = "Team lunch?", Options = ["Yes"] }))).Succeeded);
        Assert.False((await Wall(_org, asha, w => w.PollAsync(new PollInput { Question = "Team lunch?", Options = ["Yes", "yes"] }))).Succeeded);
        var id = (await Wall(_org, asha, w => w.PollAsync(new PollInput { Question = "Where for the team lunch?", Options = ["Pizza", "Biryani", "", "Salad"], Days = 2 }))).Value;

        var before = (await Wall(_org, ravi, w => w.GetAsync(id, false, false)))!.Poll!;
        Assert.Equal(3, before.Options.Count);
        Assert.False(before.ResultsVisible);
        var biryani = before.Options.Single(o => o.Text == "Biryani").Id;

        Assert.True((await Wall(_org, ravi, w => w.VoteAsync(id, biryani))).Succeeded);
        Assert.False((await Wall(_org, ravi, w => w.VoteAsync(id, biryani))).Succeeded);
        Assert.True((await Wall(_org, asha, w => w.VoteAsync(id, biryani))).Succeeded);

        var after = (await Wall(_org, ravi, w => w.GetAsync(id, false, false)))!.Poll!;
        Assert.True(after.ResultsVisible);
        Assert.Equal((2, 100, biryani), (after.TotalVotes, after.Options.Single(o => o.Id == biryani).Percent, after.MyOptionId!.Value));

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddDays(3);
        var (_, late) = await EmployeeAsync(_org, "Late");
        Assert.False((await Wall(_org, late, w => w.VoteAsync(id, biryani))).Succeeded);
        Assert.True((await Wall(_org, late, w => w.GetAsync(id, false, false)))!.Poll!.ResultsVisible); // closed polls show results
    }

    [Fact]
    public async Task Announcements_are_for_moderators_pin_to_the_top_and_notify_everyone()
    {
        var (_, asha) = await EmployeeAsync(_org, "Asha");
        var (_, ravi) = await EmployeeAsync(_org, "Ravi");
        var input = new AnnouncementPostInput { Title = "Diwali holidays", Body = "Office closed 20–22 Oct.", PinDays = 5, Notify = true };
        Assert.False((await Wall(_org, asha, w => w.AnnounceAsync(input, canModerate: false))).Succeeded);

        await Wall(_org, ravi, w => w.PostAsync(new PostInput { Body = "Earlier post" }));
        var id = (await Wall(_org, _admin, w => w.AnnounceAsync(input, canModerate: true))).Value;
        await Wall(_org, ravi, w => w.PostAsync(new PostInput { Body = "Later post" }));

        var feed = (await Wall(_org, asha, w => w.FeedAsync(1, null, false))).Items;
        Assert.Equal(id, feed[0].Id); // pinned beats newer posts
        Assert.True(feed[0].IsPinned);
        Assert.Equal(1, await UnreadAsync(_org, asha));
        Assert.Equal(1, await UnreadAsync(_org, ravi));
        Assert.Single(await Wall(_org, asha, w => w.PinnedAnnouncementsAsync(3)));

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddDays(6);
        Assert.Empty(await Wall(_org, asha, w => w.PinnedAnnouncementsAsync(3)));
        Assert.NotEqual(id, (await Wall(_org, asha, w => w.FeedAsync(1, null, false))).Items[0].Id);
    }

    // ------------------------------------------------------------------ profile changes

    [Fact]
    public async Task Profile_changes_wait_for_hr_approval()
    {
        var (emp, user) = await EmployeeAsync(_org, "Asha");
        async Task<Result> Submit(ProfileChangeInput i)
        {
            _host.Context.AsTenant(_org, user);
            return await _host.RunAsync(sp => S<IProfileChangeService>(sp).SubmitAsync(emp, i));
        }
        Task<T> Hr<T>(Func<IProfileChangeService, Task<T>> a)
        {
            _host.Context.AsTenant(_org, _admin);
            return _host.RunAsync(sp => a(S<IProfileChangeService>(sp)));
        }

        _host.Context.AsTenant(_org, user);
        var current = (await _host.RunAsync(sp => S<IProfileChangeService>(sp).CurrentAsync(emp)))!;
        Assert.False((await Submit(current)).Succeeded); // nothing changed

        current.MobilePhone = "9811111111";
        current.BloodGroup = "o+";
        Assert.True((await Submit(current)).Succeeded);
        current.MobilePhone = "9822222222";
        Assert.True((await Submit(current)).Succeeded); // replaces the first pending request

        var pending = (await Hr(s => s.ListAsync(ProfileChangeStatus.Pending, 1))).Items.Single();
        Assert.Equal(new[] { "BloodGroup", "MobilePhone" }, pending.Changes.Select(c => c.Field).Order());
        Assert.Equal(("9800000000", "9822222222"), (pending.Changes.Single(c => c.Field == "MobilePhone").From, pending.Changes.Single(c => c.Field == "MobilePhone").To));

        // The employee record does not change until HR approves; the employee can't approve their own.
        _host.Context.AsTenant(_org, user);
        Assert.False((await _host.RunAsync(sp => S<IProfileChangeService>(sp).ApproveAsync(pending.Id, null))).Succeeded);
        Assert.False((await Hr(s => s.RejectAsync(pending.Id, " "))).Succeeded);
        Assert.True((await Hr(s => s.ApproveAsync(pending.Id, "ok"))).Succeeded);
        var e = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.AsNoTracking().SingleAsync(x => x.Id == emp));
        Assert.Equal(("9822222222", "O+"), (e.MobilePhone, e.BloodGroup));
        Assert.False((await Hr(s => s.ApproveAsync(pending.Id, null))).Succeeded); // already decided

        // Other organizations cannot see or decide it.
        _host.Context.AsTenant(_other, _otherAdmin);
        Assert.Empty((await _host.RunAsync(sp => S<IProfileChangeService>(sp).ListAsync(null, 1))).Items);
        Assert.False((await _host.RunAsync(sp => S<IProfileChangeService>(sp).RejectAsync(pending.Id, "no"))).Succeeded);
    }

    // ------------------------------------------------------------------ people

    [Fact]
    public async Task Celebrations_respect_privacy_and_the_directory_lists_current_staff()
    {
        // Clock: 15 Jun 2026 (IST).
        await EmployeeAsync(_org, "Asha", dob: new DateOnly(1990, 6, 15), joined: new DateOnly(2022, 6, 18));
        var (hiddenEmp, hiddenUser) = await EmployeeAsync(_org, "Quiet", dob: new DateOnly(1991, 6, 16));
        await EmployeeAsync(_org, "Later", dob: new DateOnly(1992, 9, 1), joined: new DateOnly(2026, 6, 1));

        _host.Context.AsTenant(_org, hiddenUser);
        Assert.True((await _host.RunAsync(sp => S<IPeopleService>(sp).SavePreferencesAsync(hideBirthday: true, muteEmail: false))).Succeeded);

        _host.Context.AsTenant(_org, _admin);
        var list = await _host.RunAsync(sp => S<IPeopleService>(sp).CelebrationsAsync(7));
        Assert.Contains(list, c => c.Name == "Asha T" && c.Kind == CelebrationKind.Birthday && c.IsToday);
        Assert.Contains(list, c => c.Name == "Asha T" && c.Kind == CelebrationKind.WorkAnniversary && c.Years == 4 && c.Date == new DateOnly(2026, 6, 18));
        Assert.DoesNotContain(list, c => c.Name == "Quiet T");
        Assert.DoesNotContain(list, c => c.Name == "Later T"); // birthday outside the window; joined this year

        var dir = await _host.RunAsync(sp => S<IPeopleService>(sp).DirectoryAsync("quiet", null, 1));
        Assert.Equal("quiet@" + _org + ".test", dir.Items.Single().WorkEmail);
        _host.Context.AsTenant(_other, _otherAdmin);
        Assert.Empty((await _host.RunAsync(sp => S<IPeopleService>(sp).DirectoryAsync(null, null, 1))).Items);
    }

    // ------------------------------------------------------------------ email copies

    [Fact]
    public async Task Notification_emails_honour_preferences_and_are_sent_once()
    {
        var (raviEmp, ravi) = await EmployeeAsync(_org, "Ravi");
        var (_, muted) = await EmployeeAsync(_org, "Muted");
        _host.Context.AsTenant(_org, muted);
        await _host.RunAsync(sp => S<IPeopleService>(sp).SavePreferencesAsync(false, muteEmail: true));

        await Wall(_org, _admin, w => w.AnnounceAsync(new AnnouncementPostInput { Title = "Town hall", Body = "Friday 4 PM", PinDays = 0 }, true));
        _host.Email.Sent.Clear();

        async Task<int> Dispatch()
        {
            _host.Context.AsPlatform();
            return await _host.RunAsync(sp => S<INotificationDispatcher>(sp).RunAsync("https://hr.example.test"));
        }
        Assert.Equal(1, await Dispatch()); // Ravi only: Muted opted out; the author is not notified
        var mail = _host.Email.Sent.Single();
        Assert.Equal(($"ravi@{_org}.test", "Announcement: Town hall"), (mail.To, mail.Subject));
        Assert.Contains("https://hr.example.test/me/wall/post/", mail.HtmlBody);
        Assert.Equal(0, await Dispatch()); // never twice

        await Assert.ThrowsAsync<TenantIsolationException>(() =>
        {
            _host.Context.AsTenant(_org, ravi);
            return _host.RunAsync(sp => S<INotificationDispatcher>(sp).RunAsync(null));
        });
    }
}
