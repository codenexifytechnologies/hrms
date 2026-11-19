using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Identity;

namespace Codenexify.HRMS.Domain.CoreHr;

public enum Gender { Male, Female, Other, PreferNotToSay }
public enum MaritalStatus { Single, Married, Divorced, Widowed }
public enum EmploymentType { FullTime, PartTime, Contract, Intern, Consultant }
public enum EmployeeStatus { Probation, Active, NoticePeriod, Exited, Inactive }
public enum DocumentCategory { Identity, Address, Education, Employment, Offer, Payroll, Other, Letter }
public enum ExitType { Resignation, Termination, Retirement, EndOfContract, Absconding }
public enum ExitStatus { Pending, Approved, Rejected, Withdrawn, Completed }

/// <summary>Per-organization HR settings (exactly one row per organization).</summary>
public class OrganizationSetting : TenantEntity
{
    public string EmployeeIdPrefix { get; set; } = "EMP";
    public int EmployeeIdPadding { get; set; } = 4;
    public int NextEmployeeNumber { get; set; } = 1;
    public int DefaultNoticePeriodDays { get; set; } = 30;
    public int DefaultProbationMonths { get; set; } = 6;
    /// <summary>Gamified attendance: points for an on-time day and for a late (but present) day.</summary>
    public int OnTimePoints { get; set; } = 10;
    public int LatePoints { get; set; } = 3;
    /// <summary>GPS attendance: off, record location, or require clock-ins within an office radius.</summary>
    public Strength.GeoAttendanceMode GeoMode { get; set; }
    /// <summary>Clock-in/out from the web or app needs a camera photo (Selfie Clock-in feature).</summary>
    public bool SelfieRequired { get; set; }
    /// <summary>Selfies are deleted after this many days.</summary>
    public int SelfieRetentionDays { get; set; } = 90;
    /// <summary>Sign-in with Google / Microsoft accounts (Single Sign-On feature).</summary>
    public bool SsoGoogle { get; set; }
    public bool SsoMicrosoft { get; set; }
    /// <summary>Comma-separated email domains allowed for single sign-on (e.g. "acme.com,acme.in").</summary>
    public string? SsoDomains { get; set; }
    /// <summary>Sign-in with a one-time code sent to the user's mobile (Mobile OTP feature).</summary>
    public bool MobileOtpLogin { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public string FormatEmployeeCode(int number) => $"{EmployeeIdPrefix}{number.ToString().PadLeft(EmployeeIdPadding, '0')}";
}

public class Branch : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "India";
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public bool IsHeadOffice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Department : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public int? HeadEmployeeId { get; set; }
    public Employee? HeadEmployee { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Designation : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int? Level { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Employee : TenantEntity
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? PersonalEmail { get; set; }
    public string? MobilePhone { get; set; }

    // Personal
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? BloodGroup { get; set; }
    public string Nationality { get; set; } = "Indian";
    public string? CurrentAddress { get; set; }
    public string? PermanentAddress { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public string? EmergencyContactPhone { get; set; }
    /// <summary>PAN, encrypted at rest. Only the masked form is shown without the sensitive-data permission.</summary>
    public string? PanEncrypted { get; set; }
    public string? PanMasked { get; set; }
    public string? Uan { get; set; }

    // Employment
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? DesignationId { get; set; }
    public Designation? Designation { get; set; }
    public int? ReportingManagerId { get; set; }
    public Employee? ReportingManager { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Probation;
    public DateOnly DateOfJoining { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? DateOfExit { get; set; }
    public int NoticePeriodDays { get; set; } = 30;
    /// <summary>Employee's choice: keep their birthday out of celebrations.</summary>
    public bool HideBirthday { get; set; }
    /// <summary>Field / remote staff: geo-fence not enforced (location still recorded if enabled).</summary>
    public bool GeoFenceExempt { get; set; }

    /// <summary>Login account linked to this employee (optional).</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    public EmployeeBankDetail? BankDetail { get; set; }
    public ICollection<EmployeeEducation> Education { get; set; } = new List<EmployeeEducation>();
    public ICollection<EmployeeExperience> Experience { get; set; } = new List<EmployeeExperience>();
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();

    public string FullName => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool IsCurrent => Status is not (EmployeeStatus.Exited or EmployeeStatus.Inactive);
}

public class EmployeeBankDetail : TenantEntity
{
    public int EmployeeId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumberEncrypted { get; set; } = string.Empty;
    public string AccountNumberLast4 { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string? BranchName { get; set; }
}

public class EmployeeEducation : TenantEntity
{
    public int EmployeeId { get; set; }
    public string Qualification { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public int? YearOfPassing { get; set; }
    public string? Grade { get; set; }
}

public class EmployeeExperience : TenantEntity
{
    public int EmployeeId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeDocument : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DocumentCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>Storage key generated server-side; never accepted from clients.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public bool VisibleToEmployee { get; set; } = true;
    public string? Notes { get; set; }
}

public class ExitRequest : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public ExitType Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool InitiatedByEmployee { get; set; }
    public DateOnly RequestedOn { get; set; }
    public DateOnly RequestedLastWorkingDay { get; set; }
    public DateOnly? ApprovedLastWorkingDay { get; set; }
    public ExitStatus Status { get; set; } = ExitStatus.Pending;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
    public string? ExitInterviewNotes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ICollection<ExitChecklistItem> Checklist { get; set; } = new List<ExitChecklistItem>();
}

public class ExitChecklistItem : TenantEntity
{
    public int ExitRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Organization-defined exit checklist copied onto each approved exit.</summary>
public class ExitChecklistTemplateItem : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
