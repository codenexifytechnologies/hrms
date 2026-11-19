using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Strength;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class GeoFenceTests
{
    private static readonly GeoOffice Office = new(1, "Pune HQ", 18.520400m, 73.856700m, 200);

    [Fact]
    public void Haversine_matches_a_known_distance()
    {
        // Pune → Mumbai (city centres) is roughly 120 km as the crow flies.
        var d = GeoFence.DistanceMeters(18.5204m, 73.8567m, 19.0760m, 72.8777m);
        Assert.InRange(d, 115_000, 125_000);
        Assert.Equal(0d, GeoFence.DistanceMeters(18.5m, 73.8m, 18.5m, 73.8m), 3);
    }

    [Fact]
    public void Inside_outside_and_accuracy_allowance()
    {
        // ~150 m north of the office: inside a 200 m fence.
        var near = new GeoPoint(18.521750m, 73.856700m, 20);
        var r = GeoFence.Evaluate(near, [Office], enforce: true, exempt: false);
        Assert.Null(r.Error);
        Assert.Equal(PunchLocationStatus.Inside, r.Status);
        Assert.InRange(r.DistanceMeters!.Value, 140, 160);

        // ~260 m away is outside; a ±80 m fix pulls it inside, but the allowance is capped at 100 m.
        var edge = new GeoPoint(18.522740m, 73.856700m, null);
        Assert.Equal(PunchLocationStatus.Outside, GeoFence.Evaluate(edge, [Office], false, false).Status);
        Assert.Equal(PunchLocationStatus.Inside, GeoFence.Evaluate(edge with { AccuracyMeters = 80 }, [Office], true, false).Status);
        var far = new GeoPoint(18.524000m, 73.856700m, 450); // ~400 m, ±450 m fix: allowance only 100 m
        var farCheck = GeoFence.Evaluate(far, [Office], true, false);
        Assert.Equal(PunchLocationStatus.Outside, farCheck.Status);
        Assert.Contains("from Pune HQ", farCheck.Error);

        // Very vague fixes are refused when enforcing.
        Assert.Contains("imprecise", GeoFence.Evaluate(near with { AccuracyMeters = 900 }, [Office], true, false).Error);
    }

    [Fact]
    public void Missing_or_bogus_locations()
    {
        Assert.Contains("location is needed", GeoFence.Evaluate(null, [Office], true, false).Error);
        Assert.Equal(GeoCheck.None, GeoFence.Evaluate(null, [Office], false, false));
        // (0,0) is what broken clients send: treated as no location.
        Assert.Contains("location is needed", GeoFence.Evaluate(new GeoPoint(0, 0, 5), [Office], true, false).Error);
        Assert.Contains("No office", GeoFence.Evaluate(new GeoPoint(18.5m, 73.8m, 5), [], true, false).Error);
        // Record-only with no offices just captures the point.
        Assert.Equal(PunchLocationStatus.Captured, GeoFence.Evaluate(new GeoPoint(18.5m, 73.8m, 5), [], false, false).Status);
        // Exempt employees are never blocked (the caller passes enforce=false) and are marked as such.
        Assert.Equal(PunchLocationStatus.Exempt, GeoFence.Evaluate(new GeoPoint(19.07m, 72.87m, 5), [Office], false, true).Status);
    }
}

public sealed class StrengthTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other, _basic, _admin, _otherAdmin;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "STRENGTH");
        _other = await _host.ProvisionAsync("globex", "STRENGTH");
        _basic = await _host.ProvisionAsync("basic", "FOUNDATION");
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

    private async Task<(int EmployeeId, int UserId)> EmployeeAsync(int org, string name, int? departmentId = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "T", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = new DateOnly(2024, 1, 1), DepartmentId = departmentId },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var user = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync());
        return (r.Value, user);
    }

    private Task<T> As<TService, T>(int org, int user, Func<TService, Task<T>> a) where TService : notnull
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => a(S<TService>(sp)));
    }

    // ------------------------------------------------------------------ GPS attendance

    [Fact]
    public async Task Enforced_geo_fence_blocks_punches_outside_the_office_but_not_exempt_staff()
    {
        var (asha, ashaUser) = await EmployeeAsync(_org, "Asha");
        var (field, fieldUser) = await EmployeeAsync(_org, "Field");
        Task<Application.Common.Result> Loc(Func<IOfficeLocationService, Task<Application.Common.Result>> a) => As(_org, _admin, a);

        // Enforcing needs an office first.
        Assert.False((await Loc(l => l.SetModeAsync(GeoAttendanceMode.Enforce))).Succeeded);
        Assert.True((await Loc(l => l.SaveAsync(null, new OfficeLocationInput { Name = "Pune HQ", Latitude = 18.5204m, Longitude = 73.8567m, RadiusMeters = 200 }, multipleAllowed: true))).Succeeded);
        Assert.True((await Loc(l => l.SetModeAsync(GeoAttendanceMode.Enforce))).Succeeded);
        Assert.True((await Loc(l => l.SetExemptAsync(field, true))).Succeeded);

        Task<Application.Common.Result<PunchResult>> Punch(int emp, int user, GeoPoint? p) =>
            As<IAttendanceService, Application.Common.Result<PunchResult>>(_org, user, a => a.PunchAsync(emp, PunchSource.Web, "127.0.0.1", p));

        var noLocation = await Punch(asha, ashaUser, null);
        Assert.False(noLocation.Succeeded);
        var mumbai = await Punch(asha, ashaUser, new GeoPoint(19.0760m, 72.8777m, 15));
        Assert.False(mumbai.Succeeded);
        Assert.Contains("km from Pune HQ", mumbai.Error);
        Assert.True((await Punch(asha, ashaUser, new GeoPoint(18.5210m, 73.8570m, 15))).Succeeded);
        Assert.True((await Punch(field, fieldUser, new GeoPoint(19.0760m, 72.8777m, 15))).Succeeded);

        var punches = await _host.RunAsync(sp => S<AppDbContext>(sp).AttendancePunches.AsNoTracking().OrderBy(p => p.Id).ToListAsync());
        Assert.Equal(2, punches.Count);
        Assert.Equal((PunchLocationStatus.Inside, 18.5210m), (punches[0].LocationStatus, punches[0].Latitude!.Value));
        Assert.InRange(punches[0].DistanceMeters!.Value, 50, 90);
        Assert.Equal(PunchLocationStatus.Exempt, punches[1].LocationStatus);

        var review = await As<IOfficeLocationService, IReadOnlyList<GeoPunchRow>>(_org, _admin, l => l.RecentPunchesAsync(_host.Clock.Today));
        Assert.Equal(2, review.Count);
        // Another organization sees neither the office nor the punches.
        Assert.Empty(await As<IOfficeLocationService, IReadOnlyList<OfficeLocationRow>>(_other, _otherAdmin, l => l.ListAsync()));
        Assert.Empty(await As<IOfficeLocationService, IReadOnlyList<GeoPunchRow>>(_other, _otherAdmin, l => l.RecentPunchesAsync(_host.Clock.Today)));
    }

    [Fact]
    public async Task Location_is_not_stored_when_the_plan_lacks_gps_attendance_or_it_is_off()
    {
        var (emp, user) = await EmployeeAsync(_basic, "Basu");
        // Even if the setting were switched on directly, a plan without GPS attendance stores nothing.
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); (await db.OrganizationSettings.SingleAsync()).GeoMode = GeoAttendanceMode.Enforce; await db.SaveChangesAsync(); });
        var r = await As<IAttendanceService, Application.Common.Result<PunchResult>>(_basic, user, a => a.PunchAsync(emp, PunchSource.Web, null, new GeoPoint(19.07m, 72.87m, 10)));
        Assert.True(r.Succeeded, r.Error);

        // In a plan that has it but with the mode Off, nothing is stored either.
        var (emp2, user2) = await EmployeeAsync(_org, "Offo");
        Assert.True((await As<IAttendanceService, Application.Common.Result<PunchResult>>(_org, user2, a => a.PunchAsync(emp2, PunchSource.Web, null, new GeoPoint(19.07m, 72.87m, 10)))).Succeeded);

        _host.Context.AsPlatform();
        var rows = await _host.RunAsync(sp => S<AppDbContext>(sp).AttendancePunches.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Equal(2, rows.Count);
        Assert.All(rows, p => Assert.True(p.Latitude is null && p.Longitude is null && p.LocationStatus == PunchLocationStatus.NotCaptured));
    }

    [Fact]
    public async Task Single_location_plans_keep_one_active_office()
    {
        Task<Application.Common.Result> Save(int? id, string name, bool active = true) =>
            As<IOfficeLocationService, Application.Common.Result>(_org, _admin, l => l.SaveAsync(id, new OfficeLocationInput { Name = name, Latitude = 18.5m, Longitude = 73.8m, IsActive = active }, multipleAllowed: false));
        Assert.True((await Save(null, "One")).Succeeded);
        Assert.False((await Save(null, "Two")).Succeeded);
        Assert.True((await Save(null, "Two", active: false)).Succeeded);
    }

    // ------------------------------------------------------------------ assets

    [Fact]
    public async Task Assets_are_assigned_acknowledged_and_returned_with_history()
    {
        var (asha, ashaUser) = await EmployeeAsync(_org, "Asha");
        var (ravi, _) = await EmployeeAsync(_org, "Ravi");
        var (zed, _) = await EmployeeAsync(_other, "Zed");
        Task<T> A<T>(Func<IAssetService, Task<T>> a) => As(_org, _admin, a);

        var id = await A(s => s.SaveAsync(null, new AssetInput { AssetTag = "lap-001", Name = "Dell Latitude", Category = "Laptop", SerialNumber = "SN1" }));
        Assert.True(id.Succeeded, id.Error);
        Assert.False((await A(s => s.SaveAsync(null, new AssetInput { AssetTag = "LAP-001", Name = "Dup", Category = "Laptop" }))).Succeeded);
        Assert.Equal("LAP-001", (await A(s => s.GetAsync(id.Value)))!.AssetTag);

        Assert.False((await A(s => s.AssignAsync(id.Value, zed, null, null))).Succeeded); // other organization's employee
        Assert.True((await A(s => s.AssignAsync(id.Value, asha, null, "with charger"))).Succeeded);
        Assert.False((await A(s => s.AssignAsync(id.Value, ravi, null, null))).Succeeded); // already out

        var mine = await A(s => s.ForEmployeeAsync(asha));
        Assert.False(mine.Single().Acknowledged);
        Assert.False((await A(s => s.AcknowledgeAsync(mine.Single().AssignmentId, ravi))).Succeeded); // not Ravi's
        Assert.True((await As<IAssetService, Application.Common.Result>(_org, ashaUser, s => s.AcknowledgeAsync(mine.Single().AssignmentId, asha))).Succeeded);

        Assert.True((await A(s => s.ReturnAsync(id.Value, AssetCondition.Damaged, "cracked screen"))).Succeeded);
        var row = (await A(s => s.ListAsync(new AssetQuery()))).Items.Single();
        Assert.Equal((AssetStatus.UnderRepair, AssetCondition.Damaged, (int?)null), (row.Status, row.Condition, row.EmployeeId));
        var history = await A(s => s.HistoryAsync(id.Value));
        Assert.Equal((AssetCondition.New, AssetCondition.Damaged), (history.Single().ConditionOut, history.Single().ConditionIn));
        Assert.NotNull(history.Single().AcknowledgedAtUtc);
        Assert.Empty(await A(s => s.ForEmployeeAsync(asha)));

        // Under repair can't be handed out until it's available again.
        Assert.False((await A(s => s.AssignAsync(id.Value, ravi, null, null))).Succeeded);
        Assert.True((await A(s => s.SetStatusAsync(id.Value, AssetStatus.Available))).Succeeded);
        Assert.True((await A(s => s.AssignAsync(id.Value, ravi, null, null))).Succeeded);

        // Tenant isolation.
        Assert.Empty((await As<IAssetService, Application.Common.PagedResult<AssetRow>>(_other, _otherAdmin, s => s.ListAsync(new AssetQuery()))).Items);
        Assert.Null(await As<IAssetService, AssetInput?>(_other, _otherAdmin, s => s.GetAsync(id.Value)));
        Assert.False((await As<IAssetService, Application.Common.Result>(_other, _otherAdmin, s => s.ReturnAsync(id.Value, AssetCondition.Good, null))).Succeeded);
    }

    // ------------------------------------------------------------------ surveys

    private static SurveyInput Pulse(bool anonymous = true) => new()
    {
        Title = "June pulse", IsAnonymous = anonymous, MinResponses = 3,
        Questions =
        [
            new() { Text = "How happy are you?", Type = QuestionType.Rating },
            new() { Text = "Recommend us?", Type = QuestionType.Enps },
            new() { Text = "Best perk?", Type = QuestionType.Choice, Options = "Food\nFlexi hours\nFood" },
            new() { Text = "Anything else?", Type = QuestionType.Text, Required = false },
            new() { Text = "   " } // blank rows are ignored
        ]
    };

    [Fact]
    public async Task Anonymous_survey_answers_cannot_be_traced_to_people()
    {
        var people = new List<(int Emp, int User)>();
        foreach (var n in new[] { "Asha", "Ravi", "Meera", "Kabir" }) people.Add(await EmployeeAsync(_org, n));
        Task<T> Hr<T>(Func<ISurveyService, Task<T>> a) => As(_org, _admin, a);

        Assert.False((await Hr(s => s.CreateAsync(new SurveyInput { Title = "Empty" }, true))).Succeeded);
        Assert.False((await Hr(s => s.CreateAsync(new SurveyInput { Title = "Bad", Questions = [new() { Text = "Pick", Type = QuestionType.Choice, Options = "Only one" }] }, true))).Succeeded);
        Assert.False((await Hr(s => s.CreateAsync(new SurveyInput { Title = "Exit", Kind = SurveyKind.Exit, Questions = [new() { Text = "Why?" }] }, exitSurveysEnabled: false))).Succeeded);

        var id = (await Hr(s => s.CreateAsync(Pulse(), true))).Value;
        var question = await _host.RunAsync(sp => S<AppDbContext>(sp).SurveyQuestions.Where(q => q.SurveyId == id).OrderBy(q => q.SortOrder).Select(q => q.Id).ToListAsync());
        Assert.Equal(4, question.Count);
        Assert.Equal("Food\nFlexi hours", await _host.RunAsync(sp => S<AppDbContext>(sp).SurveyQuestions.Where(q => q.Id == question[2]).Select(q => q.Options).SingleAsync()));

        // Drafts aren't visible to employees.
        Assert.Empty(await As<ISurveyService, IReadOnlyList<SurveyRow>>(_org, people[0].User, s => s.PendingForMeAsync()));
        Assert.True((await Hr(s => s.PublishAsync(id))).Succeeded);
        Assert.Single(await As<ISurveyService, IReadOnlyList<SurveyRow>>(_org, people[0].User, s => s.PendingForMeAsync()));

        Dictionary<int, string?> Answers(int rating, int enps, string perk, string? text = null) =>
            new() { [question[0]] = rating.ToString(), [question[1]] = enps.ToString(), [question[2]] = perk, [question[3]] = text };
        Task<Application.Common.Result> Submit(int user, Dictionary<int, string?> a) => As<ISurveyService, Application.Common.Result>(_org, user, s => s.SubmitAsync(id, a));

        Assert.False((await Submit(people[0].User, new() { [question[0]] = "4" })).Succeeded); // required answers missing
        Assert.False((await Submit(people[0].User, Answers(9, 10, "Food"))).Succeeded); // rating out of range
        Assert.False((await Submit(people[0].User, Answers(4, 10, "Gym"))).Succeeded); // not an option
        Assert.True((await Submit(people[0].User, Answers(4, 10, "Food", "Great team"))).Succeeded);
        Assert.False((await Submit(people[0].User, Answers(5, 10, "Food"))).Succeeded); // once only
        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(7);
        Assert.True((await Submit(people[1].User, Answers(2, 3, "Flexi hours", "More quiet rooms"))).Succeeded);

        // Two answers: below the minimum, nothing is shown.
        var hidden = await Hr(s => s.ResultsAsync(id));
        Assert.False(hidden!.Visible);
        Assert.Empty(hidden.Questions);
        Assert.Equal((2, 4), (hidden.Survey.ResponseCount, hidden.EligibleCount));

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddHours(3);
        Assert.True((await Submit(people[2].User, Answers(5, 8, "Food"))).Succeeded);
        var results = await Hr(s => s.ResultsAsync(id));
        Assert.True(results!.Visible);
        Assert.Equal(3.7m, results.Questions[0].Average);
        Assert.Equal(0, results.Questions[1].Enps); // 1 promoter, 1 passive, 1 detractor
        Assert.Equal(2, results.Questions[2].Distribution.Single(d => d.Label == "Food").Count);
        Assert.Equal(["Great team", "More quiet rooms"], results.Questions[3].Comments);

        // Nothing stored links answers to people: no user, no author, no time of day, no audit trail.
        var stored = await _host.RunAsync(sp => S<AppDbContext>(sp).SurveyResponses.AsNoTracking().Include(r => r.Answers).ToListAsync());
        Assert.All(stored, r => Assert.True(r.RespondentUserId is null && r.RespondentName is null && r.CreatedBy is null && r.CreatedAtUtc.TimeOfDay == TimeSpan.Zero));
        Assert.All(stored.SelectMany(r => r.Answers), a => Assert.True(a.CreatedBy is null && a.CreatedAtUtc.TimeOfDay == TimeSpan.Zero));
        _host.Context.AsPlatform();
        Assert.False(await _host.RunAsync(sp => S<AppDbContext>(sp).AuditLogs.AnyAsync(a => a.EntityType.Contains("SurveyResponse") || a.EntityType.Contains("SurveyAnswer") || a.EntityType.Contains("SurveyParticipation"))));

        // Closing stops further answers; other organizations see nothing.
        Assert.True((await Hr(s => s.CloseAsync(id))).Succeeded);
        Assert.False((await Submit(people[3].User, Answers(3, 7, "Food"))).Succeeded);
        Assert.Null(await As<ISurveyService, SurveyResults?>(_other, _otherAdmin, s => s.ResultsAsync(id)));
        Assert.Empty(await As<ISurveyService, IReadOnlyList<SurveyRow>>(_other, _otherAdmin, s => s.ListAsync()));
    }

    [Fact]
    public async Task Named_surveys_target_a_department_and_close_on_time()
    {
        _host.Context.AsTenant(_org, _admin);
        var dept = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var d = new Domain.CoreHr.Department { Name = "Sales", Code = "SAL" };
            db.Departments.Add(d);
            await db.SaveChangesAsync();
            return d.Id;
        });
        var (_, sales) = await EmployeeAsync(_org, "Sana", dept);
        var (_, eng) = await EmployeeAsync(_org, "Eshan");
        var input = Pulse(anonymous: false);
        input.DepartmentId = dept;
        input.Days = 3;
        var id = (await As<ISurveyService, Application.Common.Result<int>>(_org, _admin, s => s.CreateAsync(input, true))).Value;
        Assert.True((await As<ISurveyService, Application.Common.Result>(_org, _admin, s => s.PublishAsync(id))).Succeeded);

        Assert.Single(await As<ISurveyService, IReadOnlyList<SurveyRow>>(_org, sales, s => s.PendingForMeAsync()));
        Assert.Empty(await As<ISurveyService, IReadOnlyList<SurveyRow>>(_org, eng, s => s.PendingForMeAsync()));
        Assert.Null(await As<ISurveyService, SurveyToTake?>(_org, eng, s => s.ToTakeAsync(id)));

        var take = await As<ISurveyService, SurveyToTake?>(_org, sales, s => s.ToTakeAsync(id));
        var answers = take!.Questions.Where(q => q.Required).ToDictionary(q => q.Id, q => (string?)(q.Type == QuestionType.Choice ? "Food" : "4"));
        Assert.True((await As<ISurveyService, Application.Common.Result>(_org, sales, s => s.SubmitAsync(id, answers))).Succeeded);
        // Named: the response carries the person (and the minimum is one).
        var r = await _host.RunAsync(sp => S<AppDbContext>(sp).SurveyResponses.AsNoTracking().SingleAsync());
        Assert.Equal((sales, "Sana T"), (r.RespondentUserId!.Value, r.RespondentName));
        Assert.True((await As<ISurveyService, SurveyResults?>(_org, _admin, s => s.ResultsAsync(id)))!.Visible);

        _host.Clock.AdvanceDays(4);
        Assert.Equal(SurveyStatus.Closed, (await As<ISurveyService, IReadOnlyList<SurveyRow>>(_org, _admin, s => s.ListAsync())).Single().Status);
    }

    // ------------------------------------------------------------------ anonymous feedback

    [Fact]
    public async Task Anonymous_feedback_is_reached_only_with_the_private_code()
    {
        var (_, asha) = await EmployeeAsync(_org, "Asha");
        Task<T> F<T>(int org, int user, Func<IFeedbackService, Task<T>> a) => As(org, user, a);

        var sent = await F(_org, asha, f => f.SendAsync(new FeedbackInput { Category = "Manager", Subject = "Late-night calls", Body = "Calls after 10pm every week." }));
        Assert.True(sent.Succeeded, sent.Error);
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", sent.Value);
        Assert.False((await F(_org, asha, f => f.SendAsync(new FeedbackInput { Category = "Made up", Subject = "x", Body = "0123456789" }))).Succeeded);

        // Stored without the sender, the code or the time of day.
        var thread = await _host.RunAsync(sp => S<AppDbContext>(sp).FeedbackThreads.AsNoTracking().Include(t => t.Messages).SingleAsync());
        Assert.True(thread.CreatedBy is null && thread.CreatedAtUtc.TimeOfDay == TimeSpan.Zero && thread.AccessCodeHash != sent.Value);
        Assert.True(thread.Messages.Single().CreatedBy is null && thread.Messages.Single().AuthorName is null);

        // HR replies; the sender reads it with the code (dashes and case don't matter).
        Assert.True((await F(_org, _admin, f => f.ReplyAsync(thread.Id, "Thanks, we'll talk to the team.", false))).Succeeded);
        var view = await F(_org, asha, f => f.OpenByCodeAsync(sent.Value.Replace("-", "").ToLowerInvariant()));
        Assert.Equal((FeedbackStatus.Answered, 2), (view!.Thread.Status, view.Messages.Count));
        Assert.Null(await F(_org, asha, f => f.OpenByCodeAsync("AAAA-BBBB-CCCC-DDDD")));

        // Follow-up reopens it; a refresh-resend of the same text isn't duplicated.
        Assert.True((await F(_org, asha, f => f.FollowUpAsync(sent.Value, "It happened again."))).Succeeded);
        Assert.True((await F(_org, asha, f => f.FollowUpAsync(sent.Value, "It happened again."))).Succeeded);
        Assert.Equal(3, (await F(_org, _admin, f => f.GetAsync(thread.Id)))!.Messages.Count);
        Assert.Single(await F(_org, _admin, f => f.ListAsync(openOnly: true)));

        // The code is useless in another organization, and closed threads accept nothing.
        Assert.Null(await F(_other, _otherAdmin, f => f.OpenByCodeAsync(sent.Value)));
        Assert.Null(await F(_other, _otherAdmin, f => f.GetAsync(thread.Id)));
        Assert.True((await F(_org, _admin, f => f.ReplyAsync(thread.Id, "", true))).Succeeded);
        Assert.False((await F(_org, asha, f => f.FollowUpAsync(sent.Value, "One more thing"))).Succeeded);
        Assert.Empty(await F(_org, _admin, f => f.ListAsync(openOnly: true)));

        _host.Context.AsPlatform();
        Assert.False(await _host.RunAsync(sp => S<AppDbContext>(sp).AuditLogs.AnyAsync(a => a.EntityType.Contains("Feedback"))));
    }

    // ------------------------------------------------------------------ insights

    [Fact]
    public async Task Timeline_and_people_dashboard()
    {
        var (asha, _) = await EmployeeAsync(_org, "Asha");
        await EmployeeAsync(_org, "Ravi");
        await EmployeeAsync(_other, "Zed");
        var asset = await As<IAssetService, Application.Common.Result<int>>(_org, _admin, s => s.SaveAsync(null, new AssetInput { AssetTag = "PH-1", Name = "iPhone", Category = "Phone" }));
        Assert.True((await As<IAssetService, Application.Common.Result>(_org, _admin, s => s.AssignAsync(asset.Value, asha, null, null))).Succeeded);

        var timeline = await As<IInsightsService, IReadOnlyList<TimelineEvent>>(_org, _admin, i => i.TimelineAsync(asha, includePay: false));
        Assert.Equal("Received iPhone", timeline.First().Title);
        Assert.Equal(("Joined", new DateOnly(2024, 1, 1)), (timeline.Last().Title, timeline.Last().Date));
        Assert.Empty(await As<IInsightsService, IReadOnlyList<TimelineEvent>>(_other, _otherAdmin, i => i.TimelineAsync(asha, includePay: false)));

        var dash = await As<IInsightsService, PeopleDashboard>(_org, _admin, i => i.DashboardAsync());
        Assert.Equal(2, dash.Headcount);
        Assert.Equal(12, dash.Trend.Count);
        Assert.Equal(2, dash.ByTenure.Sum(x => x.Count));
        Assert.Equal(2, dash.UnknownAge);
        Assert.Equal(1, (await As<IInsightsService, PeopleDashboard>(_other, _otherAdmin, i => i.DashboardAsync())).Headcount);
    }
}
