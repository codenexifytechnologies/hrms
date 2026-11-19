using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Engagement;

public enum PostKind { Post, Announcement, Praise, Poll }

/// <summary>An item on the organization's social wall.</summary>
public class WallPost : TenantEntity
{
    public PostKind Kind { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    /// <summary>Announcements only.</summary>
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    /// <summary>Praise only: the recognised quality, e.g. "Team player".</summary>
    public string? Badge { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedUntilUtc { get; set; }
    public DateTime? PollClosesAtUtc { get; set; }
    public bool CommentsEnabled { get; set; } = true;
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public ICollection<WallPostRecipient> Recipients { get; set; } = new List<WallPostRecipient>();
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
}

/// <summary>Employee recognised by a praise post.</summary>
public class WallPostRecipient : TenantEntity
{
    public int PostId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public class PollOption : TenantEntity
{
    public int PostId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int VoteCount { get; set; }
}

/// <summary>One vote per user per poll. Who voted is never shown, only totals.</summary>
public class PollVote : TenantEntity
{
    public int PostId { get; set; }
    public int OptionId { get; set; }
    public int UserId { get; set; }
}

public class WallComment : TenantEntity
{
    public int PostId { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class WallReaction : TenantEntity
{
    public int PostId { get; set; }
    public int UserId { get; set; }
}

public enum ProfileChangeStatus { Pending, Approved, Rejected, Cancelled }

/// <summary>An employee's request to correct personal details; applied only when HR approves.</summary>
public class ProfileChangeRequest : TenantEntity
{
    public int EmployeeId { get; set; }
    public int RequestedByUserId { get; set; }
    public ProfileChangeStatus Status { get; set; }
    /// <summary>JSON map of field → requested value.</summary>
    public string ChangesJson { get; set; } = "{}";
    /// <summary>JSON map of field → value at the time of the request (for HR to compare).</summary>
    public string PreviousJson { get; set; } = "{}";
    public string? Note { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
}
