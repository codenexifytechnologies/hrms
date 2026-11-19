using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.CoreHr;

public enum LetterKind { Offer, Appointment, Confirmation, SalaryRevision, Experience, Relieving, Other }

/// <summary>An organization's reusable letter with {{Placeholders}} filled from employee data at issue time.</summary>
public class LetterTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public LetterKind Kind { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>Plain text: blank line = new paragraph, **bold**, lines starting "- " are bullets.</summary>
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>A letter issued to an employee: the final text as issued, plus the stored PDF.</summary>
public class GeneratedLetter : TenantEntity
{
    public int EmployeeId { get; set; }
    public int? TemplateId { get; set; }
    public LetterKind Kind { get; set; }
    public string Number { get; set; } = string.Empty;
    public int FinancialYear { get; set; }
    public int Sequence { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public string? SignatoryName { get; set; }
    public string? SignatoryDesignation { get; set; }
    public int DocumentId { get; set; }
    public bool SharedWithEmployee { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
}
