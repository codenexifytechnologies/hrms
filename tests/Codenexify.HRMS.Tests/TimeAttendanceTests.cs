using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>
/// Clock: Monday 15 June 2026. Organizations use India Standard Time (UTC+5:30) and the default
/// General shift 09:30–18:30, 60 min break, 10 min grace, Sunday weekly off.
/// </summary>
public sealed class TimeAttendanceTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _otherOrg;
    private static readonly DateOnly Monday = new(2026, 6, 15);

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "GROWTH");
        _otherOrg = await _host.ProvisionAsync("globex", "GROWTH");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    /// <summary>Sets the clock to a local (IST) time.</summary>
    private void At(DateOnly date, int hour, int minute) =>
        _host.Clock.UtcNow = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc).AddMinutes(-330);

    private async Task<(int EmployeeId, int UserId)> EmployeeAsync(int org, string name, int? managerId = null, Gender? gender = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "T", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = managerId },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        if (gender is not null)
            await _host.RunAsync(sp => S<IEmployeeService>(sp).UpdatePersonalAsync(r.Value, new PersonalInput { Nationality = "Indian", Gender = gender }, false));
        var uid = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync());
        return (r.Value, uid);
    }

    private Task<int> TypeIdAsync(string code) => _host.RunAsync(sp => S<AppDbContext>(sp).LeaveTypes.Where(t => t.Code == code).Select(t => t.Id).SingleAsync());

    private Task<AttendanceDay> DayAsync(int employeeId, DateOnly date) =>
        _host.RunAsync(sp => S<AppDbContext>(sp).Attendance.AsNoTracking().SingleAsync(a => a.EmployeeId == employeeId && a.Date == date));

    [Fact]
    public void Accrual_is_prorated_and_monthly_accrual_follows_the_calendar()
    {
        var annual = new LeaveType { AnnualQuota = 12, AccrualMethod = AccrualMethod.AnnualUpfront };
        var monthly = new LeaveType { AnnualQuota = 15, AccrualMethod = AccrualMethod.Monthly };
        Assert.Equal(12, LeaveService.ExpectedAccrual(annual, new DateOnly(2020, 5, 1), 2026, Monday));
        Assert.Equal(9, LeaveService.ExpectedAccrual(annual, new DateOnly(2026, 4, 10), 2026, Monday));   // Apr–Dec
        Assert.Equal(7.5m, LeaveService.ExpectedAccrual(monthly, new DateOnly(2020, 1, 1), 2026, Monday)); // Jan–Jun
        Assert.Equal(2.5m, LeaveService.ExpectedAccrual(monthly, new DateOnly(2026, 5, 20), 2026, Monday)); // May–Jun = 2.5
        Assert.Equal(15, LeaveService.ExpectedAccrual(monthly, new DateOnly(2020, 1, 1), 2025, Monday));   // past year complete
        Assert.Equal(0, LeaveService.ExpectedAccrual(annual, new DateOnly(2027, 1, 1), 2026, Monday));
    }

    [Fact]
    public async Task Unused_leave_carries_forward_up_to_the_policy_limit()
    {
        At(Monday, 10, 0);
        var (emp, _) = await EmployeeAsync(_org, "Carry");
        _host.Context.AsTenant(_org);
        var el = await TypeIdAsync("EL");
        await _host.RunAsync(sp => S<ILeaveService>(sp).EnsureBalancesAsync(2025, [emp]));
        Assert.True((await _host.RunAsync(sp => S<ILeaveService>(sp).AdjustAsync(emp, el, 2025, 25, "Migrated balance"))).Succeeded); // 15 + 25 = 40 unused

        var balances = await _host.RunAsync(sp => S<ILeaveService>(sp).BalancesForEmployeeAsync(emp, 2026));
        var b = balances.Single(x => x.LeaveTypeId == el);
        Assert.Equal(30, b.Opening);     // EL carry-forward limit
        Assert.Equal(7.5m, b.Accrued);   // monthly, Jan–Jun
        Assert.Equal(12, balances.Single(x => x.Code == "CL").Accrued);
        Assert.Equal(0, balances.Single(x => x.Code == "CL").Opening); // CL lapses
    }

    [Fact]
    public async Task Leave_days_exclude_weekly_offs_and_holidays_and_half_days_count_half()
    {
        At(Monday, 10, 0);
        var (emp, _) = await EmployeeAsync(_org, "Days");
        _host.Context.AsTenant(_org);
        Assert.True((await _host.RunAsync(sp => S<ILeaveService>(sp).SaveHolidayAsync(null, new HolidayInput { Date = new DateOnly(2026, 6, 22), Name = "Founders Day" }))).Succeeded);
        var cl = await TypeIdAsync("CL");

        // Fri 19 – Mon 22: Sunday is a weekly off and Monday a holiday → Fri + Sat = 2 days.
        var days = await _host.RunAsync(sp => S<ILeaveService>(sp).PreviewDaysAsync(emp, new LeaveApplyInput { LeaveTypeId = cl, FromDate = new(2026, 6, 19), ToDate = new(2026, 6, 22), Reason = "x" }));
        Assert.Equal(2, days);
        var half = await _host.RunAsync(sp => S<ILeaveService>(sp).PreviewDaysAsync(emp, new LeaveApplyInput { LeaveTypeId = cl, FromDate = new(2026, 6, 19), ToDate = new(2026, 6, 19), IsHalfDay = true, HalfDaySession = HalfDaySession.FirstHalf, Reason = "x" }));
        Assert.Equal(0.5m, half);

        var onlyOff = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = cl, FromDate = new(2026, 6, 21), ToDate = new(2026, 6, 22), Reason = "x" }, true));
        Assert.False(onlyOff.Succeeded);
    }

    [Fact]
    public async Task Manager_approves_team_leave_balance_is_deducted_and_attendance_shows_leave()
    {
        At(Monday, 10, 0);
        var (mgr, mgrUser) = await EmployeeAsync(_org, "Manager");
        var (emp, empUser) = await EmployeeAsync(_org, "Member", managerId: mgr);
        var (_, outsiderUser) = await EmployeeAsync(_org, "Outsider");
        var sl = await TypeIdAsync("SL");

        _host.Context.AsTenant(_org, empUser);
        var apply = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput
            { LeaveTypeId = sl, FromDate = new(2026, 6, 11), ToDate = new(2026, 6, 12), Reason = "Fever" }, byEmployee: true));
        Assert.True(apply.Succeeded, apply.Error);

        var overlap = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput
            { LeaveTypeId = sl, FromDate = new(2026, 6, 12), ToDate = new(2026, 6, 12), Reason = "again" }, byEmployee: true));
        Assert.False(overlap.Succeeded);

        _host.Context.AsTenant(_org, mgrUser);
        Assert.Equal(1, await _host.RunAsync(sp => S<INotificationService>(sp).UnreadCountAsync()));
        Assert.Single(await _host.RunAsync(sp => S<ILeaveService>(sp).PendingForApproverAsync(mgrUser)));

        _host.Context.AsTenant(_org, outsiderUser);
        Assert.False((await _host.RunAsync(sp => S<ILeaveService>(sp).ApproveAsync(apply.Value, null, asHr: false))).Succeeded);
        _host.Context.AsTenant(_org, empUser);
        Assert.False((await _host.RunAsync(sp => S<ILeaveService>(sp).ApproveAsync(apply.Value, null, asHr: true))).Succeeded); // never your own

        _host.Context.AsTenant(_org, mgrUser);
        var approve = await _host.RunAsync(sp => S<ILeaveService>(sp).ApproveAsync(apply.Value, "Get well", asHr: false));
        Assert.True(approve.Succeeded, approve.Error);

        var bal = (await _host.RunAsync(sp => S<ILeaveService>(sp).BalancesForEmployeeAsync(emp, 2026))).Single(b => b.LeaveTypeId == sl);
        Assert.Equal(2, bal.Used);
        Assert.Equal(10, bal.Available);
        Assert.Equal(AttendanceStatus.Leave, (await DayAsync(emp, new DateOnly(2026, 6, 12))).Status);

        // Employee was notified; HR cancellation restores the balance and attendance.
        _host.Context.AsTenant(_org, empUser);
        Assert.Equal(1, await _host.RunAsync(sp => S<INotificationService>(sp).UnreadCountAsync()));
        Assert.False((await _host.RunAsync(sp => S<ILeaveService>(sp).CancelAsync(apply.Value, emp))).Succeeded); // already started
        _host.Context.AsTenant(_org, mgrUser);
        Assert.True((await _host.RunAsync(sp => S<ILeaveService>(sp).CancelAsync(apply.Value, null))).Succeeded);
        Assert.Equal(0, (await _host.RunAsync(sp => S<ILeaveService>(sp).BalancesForEmployeeAsync(emp, 2026))).Single(b => b.LeaveTypeId == sl).Used);
        Assert.Equal(AttendanceStatus.Absent, (await DayAsync(emp, new DateOnly(2026, 6, 12))).Status);
    }

    [Fact]
    public async Task Insufficient_balance_notice_and_gender_rules_are_enforced()
    {
        At(Monday, 10, 0);
        var (emp, _) = await EmployeeAsync(_org, "Rules", gender: Gender.Male);
        _host.Context.AsTenant(_org);
        var cl = await TypeIdAsync("CL");
        var tooMany = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = cl, FromDate = new(2026, 7, 1), ToDate = new(2026, 7, 31), Reason = "long" }, true));
        Assert.False(tooMany.Succeeded);
        Assert.Contains("Insufficient", tooMany.Error);

        var noNotice = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = cl, FromDate = Monday, ToDate = Monday, Reason = "today" }, byEmployee: true));
        Assert.False(noNotice.Succeeded); // CL needs 1 day notice from employees

        Assert.True((await _host.RunAsync(sp => S<ILeaveService>(sp).SaveLeaveTypeAsync(null, new LeaveTypeInput { Name = "Maternity", Code = "ML", AnnualQuota = 182, ApplicableGender = Gender.Female }))).Succeeded);
        var ml = await TypeIdAsync("ML");
        var wrongGender = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = ml, FromDate = new(2026, 7, 1), ToDate = new(2026, 7, 2), Reason = "x" }, true));
        Assert.False(wrongGender.Succeeded);

        var lop = await TypeIdAsync("LOP");
        var unpaid = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = lop, FromDate = new(2026, 7, 1), ToDate = new(2026, 7, 31), Reason = "sabbatical" }, true));
        Assert.True(unpaid.Succeeded, unpaid.Error); // unpaid leave has no balance limit
    }

    [Fact]
    public async Task Punches_compute_worked_late_and_overtime_minutes()
    {
        var (emp, _) = await EmployeeAsync(_org, "Puncher");
        _host.Context.AsTenant(_org);
        var att = (Func<IServiceProvider, IAttendanceService>)S<IAttendanceService>;

        At(Monday, 9, 55);
        var inPunch = await _host.RunAsync(sp => att(sp).PunchAsync(emp, PunchSource.Web, "10.0.0.5"));
        Assert.True(inPunch.Succeeded, inPunch.Error);
        Assert.Equal(PunchDirection.In, inPunch.Value!.Direction);
        Assert.False((await _host.RunAsync(sp => att(sp).PunchAsync(emp, PunchSource.Web, null))).Succeeded); // debounce

        At(Monday, 12, 0);
        var midday = await _host.RunAsync(sp => att(sp).TodayAsync(emp));
        Assert.True(midday.IsClockedIn);
        Assert.Equal(125, midday.WorkedMinutes);
        Assert.Equal(AttendanceStatus.Present, midday.Status); // provisional while the day is running

        At(Monday, 18, 50);
        var outPunch = await _host.RunAsync(sp => att(sp).PunchAsync(emp, PunchSource.Web, null));
        Assert.Equal(PunchDirection.Out, outPunch.Value!.Direction);

        var day = await DayAsync(emp, Monday);
        Assert.Equal(535, day.WorkedMinutes);   // 09:55 → 18:50
        Assert.Equal(25, day.LateMinutes);      // beyond 10 min grace
        Assert.Equal(55, day.OvertimeMinutes);  // 535 − 480 scheduled ≥ 30 threshold
        Assert.Equal(AttendanceStatus.Present, day.Status);

        // Next day: the unpunched Monday rule — a past working day without punches is absent; Sunday is a weekly off.
        At(Monday.AddDays(1), 8, 0);
        await _host.RunAsync(sp => att(sp).RecalculateRangeAsync([emp], Monday.AddDays(-2), Monday.AddDays(1)));
        Assert.Equal(AttendanceStatus.Absent, (await DayAsync(emp, Monday.AddDays(-2))).Status);     // Saturday (working)
        Assert.Equal(AttendanceStatus.WeeklyOff, (await DayAsync(emp, Monday.AddDays(-1))).Status);  // Sunday
        Assert.Equal(AttendanceStatus.NotMarked, (await DayAsync(emp, Monday.AddDays(1))).Status);   // today, not yet in
    }

    [Fact]
    public async Task Short_days_become_half_day_or_absent()
    {
        var (emp, _) = await EmployeeAsync(_org, "Short");
        _host.Context.AsTenant(_org);
        var att = (Func<IServiceProvider, IAttendanceService>)S<IAttendanceService>;
        var friday = new DateOnly(2026, 6, 12);
        At(friday, 9, 30); await _host.RunAsync(sp => att(sp).PunchAsync(emp, PunchSource.Web, null));
        At(friday, 14, 0); await _host.RunAsync(sp => att(sp).PunchAsync(emp, PunchSource.Web, null));
        At(Monday, 10, 0);
        await _host.RunAsync(sp => att(sp).RecalculateRangeAsync([emp], friday, friday));
        var day = await DayAsync(emp, friday);
        Assert.Equal(270, day.WorkedMinutes);
        Assert.Equal(AttendanceStatus.HalfDay, day.Status);
    }

    [Fact]
    public async Task Night_shift_punches_after_midnight_belong_to_the_shift_start_date()
    {
        var (emp, _) = await EmployeeAsync(_org, "Night");
        _host.Context.AsTenant(_org);
        Assert.True((await _host.RunAsync(sp => S<IShiftService>(sp).SaveAsync(null, new ShiftInput
            { Name = "Night", Code = "NGT", StartTime = new(22, 0), EndTime = new(6, 0), BreakMinutes = 30, MinHoursFullDay = 7, MinHoursHalfDay = 3.5m, WeeklyOffs = [DayOfWeek.Sunday] }))).Succeeded);
        var night = await _host.RunAsync(sp => S<AppDbContext>(sp).Shifts.Where(s => s.Code == "NGT").Select(s => s.Id).SingleAsync());
        At(Monday, 9, 0);
        Assert.True((await _host.RunAsync(sp => S<IShiftService>(sp).AssignAsync([emp], night, Monday))).Succeeded);

        At(Monday, 21, 58); var a = await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        At(Monday.AddDays(1), 6, 5); var b = await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        Assert.Equal(Monday, a.Value!.AttendanceDate);
        Assert.Equal(Monday, b.Value!.AttendanceDate);
        Assert.Equal(PunchDirection.Out, b.Value.Direction);
        var day = await DayAsync(emp, Monday);
        Assert.Equal(487, day.WorkedMinutes);
        Assert.Equal(0, day.LateMinutes);
        Assert.Equal(AttendanceStatus.Present, day.Status);
    }

    [Fact]
    public async Task Regularized_days_are_locked_until_cleared()
    {
        var (emp, _) = await EmployeeAsync(_org, "Regular");
        _host.Context.AsTenant(_org);
        var friday = new DateOnly(2026, 6, 12);
        At(Monday, 10, 0);
        var r = await _host.RunAsync(sp => S<IAttendanceService>(sp).RegularizeAsync(emp, new RegularizeInput
            { Date = friday, Status = AttendanceStatus.Present, InTime = new(9, 30), OutTime = new(18, 30), Remarks = "Client visit, forgot to punch" }));
        Assert.True(r.Succeeded, r.Error);
        await _host.RunAsync(sp => S<IAttendanceService>(sp).RecalculateRangeAsync([emp], friday, friday));
        var day = await DayAsync(emp, friday);
        Assert.Equal(AttendanceStatus.Present, day.Status);
        Assert.Equal(540, day.WorkedMinutes);
        Assert.True(day.IsRegularized);

        Assert.True((await _host.RunAsync(sp => S<IAttendanceService>(sp).ClearRegularizationAsync(emp, friday))).Succeeded);
        Assert.Equal(AttendanceStatus.Absent, (await DayAsync(emp, friday)).Status);
        Assert.False((await _host.RunAsync(sp => S<IAttendanceService>(sp).RegularizeAsync(emp, new RegularizeInput { Date = Monday.AddDays(3), Remarks = "future" }))).Succeeded);
    }

    [Fact]
    public async Task Overtime_claims_need_recorded_overtime_and_manager_approval()
    {
        var (mgr, mgrUser) = await EmployeeAsync(_org, "Boss");
        var (emp, empUser) = await EmployeeAsync(_org, "Worker", managerId: mgr);
        _host.Context.AsTenant(_org, empUser);
        var friday = new DateOnly(2026, 6, 12);
        At(friday, 9, 20); await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        At(friday, 20, 20); await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        Assert.Equal(180, (await DayAsync(emp, friday)).OvertimeMinutes); // 660 worked − 480

        At(Monday, 10, 0);
        Assert.False((await _host.RunAsync(sp => S<IOvertimeService>(sp).RequestAsync(emp, friday.AddDays(-1), "none recorded"))).Succeeded);
        Assert.True((await _host.RunAsync(sp => S<IOvertimeService>(sp).RequestAsync(emp, friday, "Release"))).Succeeded);
        Assert.False((await _host.RunAsync(sp => S<IOvertimeService>(sp).RequestAsync(emp, friday, "again"))).Succeeded);

        _host.Context.AsTenant(_org, mgrUser);
        var pending = Assert.Single(await _host.RunAsync(sp => S<IOvertimeService>(sp).PendingForApproverAsync(mgrUser)));
        Assert.True((await _host.RunAsync(sp => S<IOvertimeService>(sp).DecideAsync(pending.Id, true, 120, "Approved 2h", asHr: false))).Succeeded);
        var row = Assert.Single(await _host.RunAsync(sp => S<IOvertimeService>(sp).ForEmployeeAsync(emp, 2026, 6)));
        Assert.Equal(OvertimeStatus.Approved, row.Status);
        Assert.Equal(120, row.ApprovedMinutes);
    }

    [Fact]
    public async Task Gamification_counts_on_time_streaks_points_and_badges()
    {
        var (emp, _) = await EmployeeAsync(_org, "Star");
        _host.Context.AsTenant(_org);
        // On time Mon 1 – Sat 13 June except Sunday 7 (weekly off): 12 working days on time.
        for (var d = new DateOnly(2026, 6, 1); d <= new DateOnly(2026, 6, 13); d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Sunday) continue;
            At(d, 9, 30); await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
            At(d, 18, 35); await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        }
        At(Monday, 9, 50); // today: late arrival, still in progress
        await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));
        At(Monday, 11, 0);

        var score = (await _host.RunAsync(sp => S<IGamificationService>(sp).ForEmployeeAsync(emp)))!;
        Assert.Equal(12, score.OnTimeDays);
        Assert.Equal(1, score.LateDays);
        Assert.Equal(12 * 10 + 1 * 3, score.Points);
        Assert.Equal(0, score.Streak); // today's late arrival breaks the streak
        Assert.DoesNotContain(GamificationService.PunctualityPro, score.Badges);

        // Before today's late arrival the streak was 12 on-time working days (Sunday is neutral).
        At(new DateOnly(2026, 6, 13), 20, 0);
        var saturday = (await _host.RunAsync(sp => S<IGamificationService>(sp).ForEmployeeAsync(emp)))!;
        Assert.Equal(12, saturday.Streak);
        Assert.Contains(GamificationService.PunctualityPro, saturday.Badges);
        Assert.Contains(GamificationService.EarlyBird, score.Badges);
        Assert.Contains(GamificationService.IronAttendance, score.Badges);

        var board = await _host.RunAsync(sp => S<IGamificationService>(sp).LeaderboardAsync(2026, 6));
        Assert.Equal(emp, board.First().EmployeeId);
    }

    [Fact]
    public async Task Background_job_finalizes_attendance_and_accruals_for_every_organization()
    {
        At(Monday, 10, 0);
        var (a, _) = await EmployeeAsync(_org, "JobA");
        var (b, _) = await EmployeeAsync(_otherOrg, "JobB");
        _host.Context.AsAnonymous();

        var job = new Codenexify.HRMS.Infrastructure.Jobs.TimeAttendanceJob(_host.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Codenexify.HRMS.Infrastructure.Jobs.TimeAttendanceJob>.Instance,
            _host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
        await job.RunOnceAsync(CancellationToken.None);

        _host.Context.AsPlatform();
        var rows = await _host.RunAsync(sp => S<AppDbContext>(sp).Attendance.Where(x => x.EmployeeId == a || x.EmployeeId == b).ToListAsync());
        Assert.Contains(rows, r => r.EmployeeId == a && r.Date == Monday.AddDays(-2) && r.Status == AttendanceStatus.Absent);   // Saturday
        Assert.Contains(rows, r => r.EmployeeId == b && r.Date == Monday.AddDays(-1) && r.Status == AttendanceStatus.WeeklyOff); // Sunday
        Assert.True(await _host.RunAsync(sp => S<AppDbContext>(sp).LeaveBalances.AnyAsync(x => x.EmployeeId == b && x.Year == 2026)));
        Assert.Null(_host.Context.OrganizationId); // scopes were restored
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_decide_leave_and_attendance()
    {
        At(Monday, 10, 0);
        var (emp, empUser) = await EmployeeAsync(_org, "Private");
        _host.Context.AsTenant(_org, empUser);
        var sl = await TypeIdAsync("SL");
        var app = await _host.RunAsync(sp => S<ILeaveService>(sp).ApplyAsync(emp, new LeaveApplyInput { LeaveTypeId = sl, FromDate = Monday, ToDate = Monday, Reason = "x" }, true));
        Assert.True(app.Succeeded, app.Error);
        await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null));

        var (_, otherHr) = await EmployeeAsync(_otherOrg, "OtherHr");
        _host.Context.AsTenant(_otherOrg, otherHr);
        Assert.False((await _host.RunAsync(sp => S<ILeaveService>(sp).ApproveAsync(app.Value, null, asHr: true))).Succeeded);
        Assert.Empty((await _host.RunAsync(sp => S<ILeaveService>(sp).RequestsAsync(new LeaveRequestQuery { Status = null }))).Items);
        Assert.False((await _host.RunAsync(sp => S<IAttendanceService>(sp).PunchAsync(emp, PunchSource.Web, null))).Succeeded);
        var (rows, _) = await _host.RunAsync(sp => S<IAttendanceService>(sp).DayAsync(Monday, null, null));
        Assert.DoesNotContain(rows, r => r.EmployeeId == emp);
    }
}
