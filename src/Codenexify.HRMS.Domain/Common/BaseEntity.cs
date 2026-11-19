namespace Codenexify.HRMS.Domain.Common;

/// <summary>Base for every persisted entity: identity key + creation/modification stamps.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Rows are never physically removed; a global query filter hides deleted rows.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>Row belongs to exactly one client organization.</summary>
public interface ITenantEntity
{
    int OrganizationId { get; set; }
}

/// <summary>Changes to this entity are written to the audit trail automatically.</summary>
public interface IAuditable { }

/// <summary>
/// Rows that must not reveal who wrote them (anonymous feedback, anonymous survey answers): no CreatedBy/UpdatedBy,
/// date-only timestamps, and never written to the audit trail — even though the writer is signed in.
/// </summary>
public interface IAnonymousRecord { }

/// <summary>Base for all tenant-owned business data (the large majority of HRMS tables).</summary>
public abstract class TenantEntity : BaseEntity, ITenantEntity, ISoftDelete, IAuditable
{
    public int OrganizationId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
