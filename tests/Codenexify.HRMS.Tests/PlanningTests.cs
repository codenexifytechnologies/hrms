using System.Text;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Planning;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Planning;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Headcount planning and pre-boarding. Clock: 15 June 2026.</summary>
public sealed class PlanningTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin, _sales, _ops;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("plan", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@plan.test").Select(u => u.Id).SingleAsync());
        _host.Context.AsTenant(_org, _admin);
        (_sales, _ops) = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var s = new Department { OrganizationId = _org, Name = "Sales" };
            var o = new Department { OrganizationId = _org, Name = "Ops" };
            db.Departments.AddRange(s, o);
            await db.SaveChangesAsync();
            return (s.Id, o.Id);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? department, DateOnly joined)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "P", WorkEmail = $"{name.ToLowerInvariant()}@plan.test", DateOfJoining = joined, DepartmentId = department },
            CreateLogin = false
        }));
        Assert.True(r.Succeeded, r.Error);
        return r.Value;
    }

    private Task<T> H<T>(Func<IHeadcountService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IHeadcountService>(sp))); }
    private Task<T> P<T>(Func<IPreboardingService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IPreboardingService>(sp))); }
    private Task<T> Anon<T>(Func<IPreboardingService, Task<T>> a) { _host.Context.AsAnonymous(); return _host.RunAsync(sp => a(S<IPreboardingService>(sp))); }

    private static UploadedFile Pdf() => new(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 test")), "pan.pdf", 13);

    // ------------------------------------------------------------------ headcount

    [Fact]
    public async Task Plan_summarises_departments_and_tracks_hires_against_openings()
    {
        await HireAsync("Old", _sales, new DateOnly(2025, 1, 1));
        var plan = await H(s => s.SavePlanAsync(null, new HeadcountPlanInput { Name = "FY27", StartsOn = new DateOnly(2026, 4, 1), EndsOn = new DateOnly(2027, 3, 31) }));
        Assert.True(plan.Succeeded, plan.Error);
        var reps = await H(s => s.SavePositionAsync(plan.Value, null, new HeadcountPositionInput
        {
            Title = "Sales rep", DepartmentId = _sales, Openings = 3, TargetStartOn = new DateOnly(2026, 7, 1), AnnualBudgetPerHire = 600000
        }));
        Assert.True(reps.Succeeded, reps.Error);
        await H(s => s.SavePositionAsync(plan.Value, null, new HeadcountPositionInput
        {
            Title = "Backfill", DepartmentId = _sales, Kind = HeadcountPositionKind.Replacement, Openings = 1, TargetStartOn = new DateOnly(2026, 8, 1), AnnualBudgetPerHire = 500000
        }));

        // Outside the plan dates / unknown department.
        Assert.False((await H(s => s.SavePositionAsync(plan.Value, null, new HeadcountPositionInput { Title = "x", DepartmentId = _sales, TargetStartOn = new DateOnly(2027, 5, 1) }))).Succeeded);
        Assert.False((await H(s => s.SavePositionAsync(plan.Value, null, new HeadcountPositionInput { Title = "x", DepartmentId = 9999, TargetStartOn = new DateOnly(2026, 7, 1) }))).Succeeded);

        var newbie = await HireAsync("Nina", _sales, new DateOnly(2026, 6, 1));
        var later = await HireAsync("Leo", _sales, new DateOnly(2026, 7, 10)); // joins after "today"
        var opsGuy = await HireAsync("Omar", _ops, new DateOnly(2026, 6, 1));

        Assert.True((await H(s => s.LinkHireAsync(plan.Value, reps.Value, newbie))).Succeeded);
        Assert.True((await H(s => s.LinkHireAsync(plan.Value, reps.Value, later))).Succeeded);
        Assert.False((await H(s => s.LinkHireAsync(plan.Value, reps.Value, newbie))).Succeeded); // already linked
        Assert.False((await H(s => s.LinkHireAsync(plan.Value, reps.Value, opsGuy))).Succeeded); // other department
        Assert.False((await H(s => s.SavePositionAsync(plan.Value, reps.Value, new HeadcountPositionInput
        {
            Title = "Sales rep", DepartmentId = _sales, Openings = 1, TargetStartOn = new DateOnly(2026, 7, 1)
        }))).Succeeded); // below the two hired

        var v = (await H(s => s.ViewAsync(plan.Value, payrollAllowed: false)))!;
        var sales = Assert.Single(v.Departments);
        Assert.Equal(2, sales.Today);         // Old + Nina
        Assert.Equal(1, sales.JoiningSoon);   // Leo
        Assert.Equal(3, sales.NewOpenings);
        Assert.Equal(1, sales.ReplacementOpenings);
        Assert.Equal(2, sales.Filled);
        Assert.Equal(2, sales.Remaining);     // 1 rep + 1 backfill
        Assert.Equal(5, sales.Projected);     // 2 today + 1 joining + 2 remaining
        Assert.Equal(2_300_000m, sales.Budget);
        Assert.Null(sales.Committed);          // no payroll access
        Assert.Contains(v.UnlinkedJoiners, j => j.EmployeeId == opsGuy);
        Assert.DoesNotContain(v.UnlinkedJoiners, j => j.EmployeeId == newbie);

        var row = Assert.Single(await H(s => s.PlansAsync()));
        Assert.Equal((4, 2, 2_300_000m), (row.Openings, row.Filled, row.Budget));
        Assert.NotNull(await H(s => s.ExportAsync(plan.Value, true)));
    }

    [Fact]
    public async Task Plan_status_moves_and_closed_plans_are_read_only()
    {
        var plan = (await H(s => s.SavePlanAsync(null, new HeadcountPlanInput { Name = "H2", StartsOn = new DateOnly(2026, 7, 1), EndsOn = new DateOnly(2026, 12, 31) }))).Value;
        Assert.False((await H(s => s.SetStatusAsync(plan, "approve"))).Succeeded); // no positions yet
        var pos = (await H(s => s.SavePositionAsync(plan, null, new HeadcountPositionInput { Title = "Analyst", DepartmentId = _ops, TargetStartOn = new DateOnly(2026, 9, 1) }))).Value;
        Assert.True((await H(s => s.SetStatusAsync(plan, "approve"))).Succeeded);
        Assert.False((await H(s => s.DeletePlanAsync(plan))).Succeeded); // only drafts
        Assert.True((await H(s => s.SetStatusAsync(plan, "close"))).Succeeded);
        Assert.False((await H(s => s.DeletePositionAsync(plan, pos))).Succeeded);
        Assert.False((await H(s => s.SavePlanAsync(plan, new HeadcountPlanInput { Name = "x", StartsOn = new DateOnly(2026, 7, 1), EndsOn = new DateOnly(2026, 12, 31) }))).Succeeded);
        Assert.True((await H(s => s.SetStatusAsync(plan, "reopen"))).Succeeded);
        Assert.True((await H(s => s.SetStatusAsync(plan, "draft"))).Succeeded);
        Assert.True((await H(s => s.DeletePlanAsync(plan))).Succeeded);
        Assert.False((await H(s => s.SavePlanAsync(null, new HeadcountPlanInput { Name = "Bad", StartsOn = new DateOnly(2026, 7, 1), EndsOn = new DateOnly(2026, 6, 1) }))).Succeeded);
    }

    // ------------------------------------------------------------------ pre-boarding

    private static string TokenOf(string link) => link[(link.IndexOf("token=", StringComparison.Ordinal) + 6)..];

    [Fact]
    public async Task New_hire_completes_preboarding_through_the_link_without_signing_in()
    {
        await P(s => s.SaveSettingsAsync(new PreboardingSettingsInput { WelcomeMessage = "Welcome aboard!", AskBankDetails = true, AskPan = true, LinkDaysAfterJoining = 2 }));
        var doc = (await P(s => s.SaveItemAsync(null, new PreboardingItemInput { Title = "PAN card copy", Kind = PreboardingItemKind.Document }))).Value;
        var ack = (await P(s => s.SaveItemAsync(null, new PreboardingItemInput { Title = "Code of conduct", Kind = PreboardingItemKind.Acknowledgement, Description = "Be kind." }))).Value;
        await P(s => s.SaveItemAsync(null, new PreboardingItemInput { Title = "Photo", Kind = PreboardingItemKind.Document, IsRequired = false }));

        var asha = await HireAsync("Asha", _sales, new DateOnly(2026, 7, 1));
        Assert.False((await P(s => s.SendAsync(asha, "not-an-email", "https://hr.test"))).Succeeded);
        var sent = await P(s => s.SendAsync(asha, "Asha.Personal@mail.test", "https://hr.test"));
        Assert.True(sent.Succeeded, sent.Error);
        var mail = Assert.Single(_host.Email.Sent, m => m.Category == "Preboarding");
        Assert.Equal("asha.personal@mail.test", mail.To);
        Assert.Contains(sent.Value!, mail.HtmlBody);
        var token = TokenOf(sent.Value!);

        // The new hire, not signed in.
        var portal = await Anon(s => s.PortalAsync(token));
        Assert.NotNull(portal);
        Assert.Equal("Asha", portal!.FirstName);
        Assert.Equal("Sales", portal.Department);
        Assert.False(portal.CanSubmit);
        Assert.Equal(PreboardingState.Opened, (await P(s => s.StatusAsync(asha)))!.State);

        Assert.False((await Anon(s => s.SubmitAsync(token))).Succeeded); // nothing done yet
        Assert.True((await Anon(s => s.SavePersonalAsync(token, new PreboardingPersonalInput
        {
            DateOfBirth = new DateOnly(1998, 2, 3), CurrentAddress = "12 MG Road, Pune", EmergencyContactName = "Ravi", EmergencyContactRelation = "Brother",
            EmergencyContactPhone = "9876543210", Pan = "ABCDE1234F"
        }))).Succeeded);
        Assert.True((await Anon(s => s.SaveBankAsync(token, new BankInput { BankName = "HDFC", AccountHolderName = "Asha P", AccountNumber = "001122334455", Ifsc = "HDFC0001234" }))).Succeeded);
        Assert.True((await Anon(s => s.UploadAsync(token, doc, Pdf()))).Succeeded);
        Assert.True((await Anon(s => s.UploadAsync(token, doc, Pdf()))).Succeeded); // replaces the unchecked copy
        Assert.True((await Anon(s => s.AcknowledgeAsync(token, ack))).Succeeded);
        Assert.False((await Anon(s => s.UploadAsync(token, ack, Pdf()))).Succeeded); // not a document item

        Assert.True((await Anon(s => s.PortalAsync(token)))!.CanSubmit); // the photo is optional
        Assert.True((await Anon(s => s.SubmitAsync(token))).Succeeded);
        Assert.False((await Anon(s => s.SavePersonalAsync(token, new PreboardingPersonalInput()))).Succeeded); // locked after submitting

        _host.Context.AsTenant(_org, _admin);
        var e = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Include(x => x.Documents).SingleAsync(x => x.Id == asha));
        Assert.Equal("12 MG Road, Pune", e.CurrentAddress);
        Assert.Equal("12 MG Road, Pune", e.PermanentAddress); // same when left blank
        Assert.Equal("XXXXXX234F", e.PanMasked);
        Assert.Single(e.Documents); // the replaced copy is gone
        var status = (await P(s => s.StatusAsync(asha)))!;
        Assert.Equal(PreboardingState.Submitted, status.State);
        Assert.Equal(e.Documents.Single().Id, status.Steps.Single(x => x.ItemId == doc).DocumentId);
        Assert.Contains(await P(s => s.JoinersAsync()), j => j.EmployeeId == asha && j.Done == 4 && j.Total == 5);

        // HR reopens; a later link carries the answers over and the old one dies.
        Assert.True((await P(s => s.ReopenAsync(asha))).Succeeded);
        var again = (await P(s => s.SendAsync(asha, "asha.personal@mail.test", "https://hr.test"))).Value!;
        Assert.Null(await Anon(s => s.PortalAsync(token)));
        Assert.True((await Anon(s => s.PortalAsync(TokenOf(again))))!.PersonalDone);
    }

    [Fact]
    public async Task Links_stop_working_when_revoked_expired_or_for_joined_people()
    {
        var past = await HireAsync("Past", _sales, new DateOnly(2026, 6, 1));
        Assert.False((await P(s => s.SendAsync(past, "p@mail.test", "https://hr.test"))).Succeeded); // already joined

        var kiran = await HireAsync("Kiran", _sales, new DateOnly(2026, 6, 20));
        var link = (await P(s => s.SendAsync(kiran, "k@mail.test", "https://hr.test"))).Value!;
        var token = TokenOf(link);
        Assert.NotNull(await Anon(s => s.PortalAsync(token)));
        Assert.Null(await Anon(s => s.PortalAsync(token + "x")));
        Assert.Null(await Anon(s => s.PortalAsync("")));

        // Default: works until 3 days after joining.
        _host.Clock.UtcNow = new DateTime(2026, 6, 23, 20, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(await Anon(s => s.PortalAsync(token)));
        _host.Clock.UtcNow = new DateTime(2026, 6, 24, 0, 0, 1, DateTimeKind.Utc);
        Assert.Null(await Anon(s => s.PortalAsync(token)));
        Assert.Equal(PreboardingState.Expired, (await P(s => s.StatusAsync(kiran)))!.State);

        _host.Clock.UtcNow = new DateTime(2026, 6, 15, 6, 0, 0, DateTimeKind.Utc);
        Assert.True((await P(s => s.RevokeAsync(kiran))).Succeeded);
        Assert.Null(await Anon(s => s.PortalAsync(token)));
        Assert.Equal(PreboardingState.Revoked, (await P(s => s.StatusAsync(kiran)))!.State);

        // Another organization's token never opens in this one, and HR of one org can't see the other's invites.
        var other = await _host.ProvisionAsync("other", "GROWTH", trial: false);
        _host.Context.AsTenant(other);
        Assert.Empty(await _host.RunAsync(sp => S<AppDbContext>(sp).PreboardingInvites.ToListAsync()));
    }
}
