using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Strength;

// ====================================================================== assets

public enum AssetStatus { Available, Assigned, UnderRepair, Retired, Lost }
public enum AssetCondition { New, Good, Fair, Damaged }

public class Asset : TenantEntity
{
    public string AssetTag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Description { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Available;
    public AssetCondition Condition { get; set; } = AssetCondition.New;
    public int? CurrentEmployeeId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class AssetAssignment : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int EmployeeId { get; set; }
    public DateOnly AssignedOn { get; set; }
    public DateOnly? ExpectedReturnOn { get; set; }
    public AssetCondition ConditionOut { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public AssetCondition? ConditionIn { get; set; }
    public string? ReturnedTo { get; set; }
    public string? Notes { get; set; }
}

// ====================================================================== locations

public enum GeoAttendanceMode { Off, Record, Enforce }
public enum PunchLocationStatus { NotCaptured, Captured, Inside, Outside, Exempt }

public class OfficeLocation : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; } = 200;
    public bool IsActive { get; set; } = true;
}

// ====================================================================== surveys

public enum SurveyKind { Pulse, Survey, Exit }
public enum SurveyStatus { Draft, Open, Closed }
public enum QuestionType { Rating, Choice, Text, Enps }

public class Survey : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SurveyKind Kind { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public SurveyStatus Status { get; set; }
    /// <summary>Null = everyone; otherwise one department.</summary>
    public int? DepartmentId { get; set; }
    /// <summary>How long the survey stays open once published.</summary>
    public int DurationDays { get; set; } = 7;
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClosesAtUtc { get; set; }
    /// <summary>Results stay hidden until at least this many responses (protects anonymity).</summary>
    public int MinResponses { get; set; } = 3;
    public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
}

public class SurveyQuestion : TenantEntity
{
    public int SurveyId { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    /// <summary>Choice options, one per line.</summary>
    public string? Options { get; set; }
    public bool Required { get; set; } = true;
}

/// <summary>
/// Records only that a user has answered (prevents double voting). Deliberately has no identity column and no
/// timestamps (key = survey + user), so the order people answered in can't be matched to the order of responses.
/// </summary>
public class SurveyParticipation : ITenantEntity, ISoftDelete
{
    public int OrganizationId { get; set; }
    public int SurveyId { get; set; }
    public int UserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

public class SurveyResponse : TenantEntity, IAnonymousRecord
{
    public int SurveyId { get; set; }
    /// <summary>Null for anonymous surveys.</summary>
    public int? RespondentUserId { get; set; }
    public string? RespondentName { get; set; }
    /// <summary>Date only (no time) so anonymous answers can't be matched to activity logs.</summary>
    public DateOnly SubmittedOn { get; set; }
    public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
}

public class SurveyAnswer : TenantEntity, IAnonymousRecord
{
    public int ResponseId { get; set; }
    public int QuestionId { get; set; }
    public int? Number { get; set; }
    public string? Text { get; set; }
}

// ====================================================================== anonymous feedback

public enum FeedbackStatus { Open, Answered, Closed }

/// <summary>A confidential message to HR. Carries no identity; the sender follows up with a private code.</summary>
public class FeedbackThread : TenantEntity, IAnonymousRecord
{
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public FeedbackStatus Status { get; set; }
    /// <summary>SHA-256 of the sender's private access code.</summary>
    public string AccessCodeHash { get; set; } = string.Empty;
    public DateOnly CreatedOn { get; set; }
    public ICollection<FeedbackMessage> Messages { get; set; } = new List<FeedbackMessage>();
}

public class FeedbackMessage : TenantEntity, IAnonymousRecord
{
    public int ThreadId { get; set; }
    public bool FromHr { get; set; }
    /// <summary>HR replier's name; null for the anonymous sender.</summary>
    public string? AuthorName { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateOnly SentOn { get; set; }
}
