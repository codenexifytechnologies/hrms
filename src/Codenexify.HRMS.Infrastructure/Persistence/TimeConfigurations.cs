using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        TenantConfig.Base(b, "Holidays");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Date });
    }
}

internal sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> b)
    {
        TenantConfig.Base(b, "LeaveTypes");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.Color).HasMaxLength(7);
        b.Property(x => x.AnnualQuota).HasPrecision(6, 2);
        b.Property(x => x.CarryForwardLimit).HasPrecision(6, 2);
        b.Property(x => x.MaxEncashableDaysPerYear).HasPrecision(6, 2);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> b)
    {
        TenantConfig.Base(b, "LeaveBalances");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in new[] { nameof(LeaveBalance.Opening), nameof(LeaveBalance.Accrued), nameof(LeaveBalance.Adjusted), nameof(LeaveBalance.Used), nameof(LeaveBalance.Encashed), nameof(LeaveBalance.EncashmentPaid) })
            b.Property(p).HasPrecision(6, 2);
        b.Ignore(x => x.Available);
        b.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class LeaveApplicationConfiguration : IEntityTypeConfiguration<LeaveApplication>
{
    public void Configure(EntityTypeBuilder<LeaveApplication> b)
    {
        TenantConfig.Base(b, "LeaveApplications");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Days).HasPrecision(6, 2);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.DecidedBy).HasMaxLength(200);
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.EmployeeId, x.FromDate, x.ToDate });
        b.HasIndex(x => new { x.ApproverUserId, x.Status });
    }
}

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        TenantConfig.Base(b, "Shifts");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.WeeklyOffs).HasMaxLength(100).IsRequired();
        b.Property(x => x.MinHoursFullDay).HasPrecision(4, 2);
        b.Property(x => x.MinHoursHalfDay).HasPrecision(4, 2);
        b.Ignore(x => x.CrossesMidnight);
        b.Ignore(x => x.ScheduledMinutes);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> b)
    {
        TenantConfig.Base(b, "ShiftAssignments");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
    }
}

internal sealed class AttendancePunchConfiguration : IEntityTypeConfiguration<AttendancePunch>
{
    public void Configure(EntityTypeBuilder<AttendancePunch> b)
    {
        TenantConfig.Base(b, "AttendancePunches");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Note).HasMaxLength(300);
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.Property(x => x.LocationStatus).HasMaxLength(20).HasDefaultValue(Domain.Strength.PunchLocationStatus.NotCaptured);
        b.Property(x => x.SelfieStorageKey).HasMaxLength(300);
        b.HasOne<Domain.Strength.OfficeLocation>().WithMany().HasForeignKey(x => x.OfficeLocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.AttendanceDate });
    }
}

internal sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> b)
    {
        TenantConfig.Base(b, "Attendance");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Remarks).HasMaxLength(300);
        b.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.OrganizationId, x.Date });
    }
}

internal sealed class OvertimeRequestConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> b)
    {
        TenantConfig.Base(b, "OvertimeRequests");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.DecidedBy).HasMaxLength(200);
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.HasIndex(x => new { x.EmployeeId, x.Date });
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        TenantConfig.Base(b, "Notifications");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        b.Property(x => x.Link).HasMaxLength(300);
        b.HasIndex(x => new { x.UserId, x.IsRead });
        b.HasIndex(x => x.EmailPending).HasFilter("[EmailPending] = 1");
    }
}
