using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Common;

namespace Codenexify.HRMS.Application.Support;

/// <summary>
/// Control Center "Reset all data": deletes every organization and everything inside it, keeping Control Center
/// users and platform setup (plans, modules, permissions, statutory rates, Control Center settings).
/// </summary>
public static class DataResetRules
{
    /// <summary>The exact phrase the Super Admin must type to confirm.</summary>
    public const string ConfirmationPhrase = "DELETE ALL COMPANY DATA";

    /// <summary>Configuration switch; off unless set to true (the Development settings file turns it on).</summary>
    public const string EnabledSetting = "Platform:AllowDataReset";

    /// <summary>Optional folder for the automatic backup; defaults to SQL Server's own backup folder.</summary>
    public const string BackupFolderSetting = "Platform:DataResetBackupFolder";

    /// <summary>Tables that survive a reset. Everything else in the data model is emptied.</summary>
    public static readonly IReadOnlySet<string> KeptTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
        "PlatformUsers", "PlatformUserPermissions", "PlatformSettings", "PlatformAnnouncements", "LegalPages",
        "SubscriptionPlans", "PlanFeatures", "Features", "Permissions",
        "StatutoryRateSets", "ProfessionalTaxSlabs", "LabourWelfareFundRules", "IncomeTaxRules", "IncomeTaxSlabs", "IncomeTaxSurcharges",
    };
}

public sealed class DataResetInput
{
    [Required(ErrorMessage = "Type the confirmation phrase."), Display(Name = "Confirmation phrase")]
    public string Confirmation { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password), Display(Name = "Your password")]
    public string Password { get; set; } = string.Empty;

    [StringLength(10), Display(Name = "Authenticator code")]
    public string? TotpCode { get; set; }
}

/// <summary>What the page shows before anything is deleted.</summary>
public sealed record DataResetStatus(
    bool Enabled, bool IsSuperAdmin, bool TwoFactorRequired, bool BackupSupported,
    int Organizations, int TenantUsers, int Employees, int UploadedFiles);

public sealed record DataResetOutcome(int TablesEmptied, string? BackupFile, string? UploadsMovedTo, string? Warning);

public interface IDataResetService
{
    Task<DataResetStatus> StatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Verifies the caller (active Super Admin, password, authenticator code when 2FA is on, exact phrase), takes and
    /// verifies a database backup, then empties every non-platform table in one transaction. Nothing is deleted
    /// if any check or the backup fails.
    /// </summary>
    Task<Result<DataResetOutcome>> ResetAsync(DataResetInput input, CancellationToken ct = default);
}
