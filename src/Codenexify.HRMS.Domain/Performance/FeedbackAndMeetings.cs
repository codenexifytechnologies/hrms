using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== continuous feedback

public enum FeedbackTone { Appreciation, Suggestion }

/// <summary>Who can read a piece of feedback besides its author.</summary>
public enum FeedbackVisibility
{
    /// <summary>Only the person it is about.</summary>
    Private,
    /// <summary>The person and their managers.</summary>
    PersonAndManagers,
    /// <summary>Only the person's managers (a note to the manager, not shown to the person).</summary>
    ManagersOnly
}

public class FeedbackNote : TenantEntity
{
    public int AuthorEmployeeId { get; set; }
    public int AboutEmployeeId { get; set; }
    public FeedbackTone Tone { get; set; }
    public FeedbackVisibility Visibility { get; set; }
    public string Body { get; set; } = string.Empty;
    /// <summary>Set when written in answer to a request.</summary>
    public int? FeedbackRequestId { get; set; }
    public DateTime AtUtc { get; set; }
}

public enum FeedbackRequestStatus { Pending, Answered, Declined }

/// <summary>Someone asks colleagues for feedback about themselves (or a manager about a team member).</summary>
public class FeedbackRequest : TenantEntity
{
    public int RequestedByEmployeeId { get; set; }
    public int AboutEmployeeId { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateOnly? DueOn { get; set; }
    public DateTime AtUtc { get; set; }
    public ICollection<FeedbackRequestRecipient> Recipients { get; set; } = new List<FeedbackRequestRecipient>();
}

public class FeedbackRequestRecipient : TenantEntity
{
    public int FeedbackRequestId { get; set; }
    public int EmployeeId { get; set; }
    public FeedbackRequestStatus Status { get; set; } = FeedbackRequestStatus.Pending;
    public DateTime? RespondedAtUtc { get; set; }
}

// ====================================================================== one-on-ones

public enum MeetingStatus { Scheduled, Completed, Cancelled }
public enum MeetingRepeat { None, Weekly, Fortnightly, Monthly }

/// <summary>A 1:1 between a manager and someone in their reporting line. Times are in the organization's local time.</summary>
public class OneOnOne : TenantEntity
{
    public int ManagerEmployeeId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly OnDate { get; set; }
    public TimeOnly StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public MeetingRepeat Repeat { get; set; }
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    /// <summary>Notes both people can see and edit.</summary>
    public string? SharedNotes { get; set; }
    /// <summary>Only the manager sees these.</summary>
    public string? ManagerPrivateNotes { get; set; }
    /// <summary>Only the employee sees these.</summary>
    public string? EmployeePrivateNotes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ICollection<MeetingItem> Items { get; set; } = new List<MeetingItem>();
    public byte[] RowVersion { get; set; } = [];
}

public enum MeetingItemKind { Agenda, Action }

/// <summary>An agenda topic or an action item. Open action items carry over to the pair's next meeting.</summary>
public class MeetingItem : TenantEntity
{
    public int OneOnOneId { get; set; }
    public MeetingItemKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public int AddedByEmployeeId { get; set; }
    /// <summary>Action items: who does it.</summary>
    public int? OwnerEmployeeId { get; set; }
    public DateOnly? DueOn { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAtUtc { get; set; }
}
