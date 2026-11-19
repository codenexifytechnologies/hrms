using System.Data;
using System.Data.Common;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Services;

/// <summary>
/// Empties every company-side table (see <see cref="DataResetRules.KeptTables"/> for what survives).
/// SQL Server: a verified COPY_ONLY backup is taken first; any failure before the commit leaves the data untouched.
/// </summary>
public sealed class DataResetService(
    AppDbContext db, ICurrentContext context, IPasswordService passwords, ITotpService totp, ISecretProtector protector,
    IClock clock, IConfiguration config, IFileStorage storage, IFeatureAccessService featureAccess, IAuditWriter audit,
    ILogger<DataResetService> logger) : IDataResetService
{
    private const int CommandTimeoutSeconds = 900;

    private bool Enabled => config.GetValue<bool>(DataResetRules.EnabledSetting);
    private bool IsSqlServer => db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
    private bool IsSqlite => db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    private string? UploadsRoot => (storage as LocalFileStorage)?.Root;

    public async Task<DataResetStatus> StatusAsync(CancellationToken ct = default)
    {
        var actor = await ActorAsync(ct);
        var files = 0;
        if (UploadsRoot is { } root && Directory.Exists(root))
            files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count();
        return new DataResetStatus(
            Enabled, actor is { Role: PlatformRole.SuperAdmin, IsActive: true }, actor?.TwoFactorEnabled == true, IsSqlServer,
            await db.Organizations.IgnoreQueryFilters().CountAsync(ct),
            await db.Users.IgnoreQueryFilters().CountAsync(ct),
            await db.Employees.IgnoreQueryFilters().CountAsync(ct),
            files);
    }

    public async Task<Result<DataResetOutcome>> ResetAsync(DataResetInput input, CancellationToken ct = default)
    {
        if (!Enabled)
            return Fail($"Reset is switched off on this server. Set \"{DataResetRules.EnabledSetting}\" to true in the settings file to allow it.");
        var actor = await ActorAsync(ct);
        if (actor is not { Role: PlatformRole.SuperAdmin, IsActive: true })
            return Fail("Only an active Super Admin can reset all data.");
        if (!string.Equals(input.Confirmation?.Trim(), DataResetRules.ConfirmationPhrase, StringComparison.Ordinal))
            return Fail($"Type the confirmation phrase exactly: {DataResetRules.ConfirmationPhrase}");
        if (string.IsNullOrEmpty(input.Password) || !passwords.Verify(actor.PasswordHash, input.Password).Verified)
        {
            logger.LogWarning("Data reset refused for platform user {UserId}: wrong password", actor.Id);
            return Fail("Your password is incorrect.");
        }
        if (actor.TwoFactorEnabled)
        {
            var code = new string((input.TotpCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (actor.TwoFactorSecret is null || code.Length == 0 || !totp.Verify(protector.Unprotect(actor.TwoFactorSecret), code, clock.UtcNow))
                return Fail("The authenticator code is not valid.");
        }

        var plan = BuildPlan();
        if (plan.Error is { } planError) return Fail(planError);

        var stamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss");
        db.Database.SetCommandTimeout(CommandTimeoutSeconds);

        string? backup = null;
        if (IsSqlServer)
        {
            try { backup = await BackupAsync(stamp, ct); }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                logger.LogError(ex, "Data reset aborted: backup failed");
                return Fail($"The backup failed, so nothing was deleted. {ex.Message}");
            }
        }
        else if (!IsSqlite)
        {
            return Fail($"Reset is not supported on database provider {db.Database.ProviderName}.");
        }

        var actorId = actor.Id;
        db.ChangeTracker.Clear();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (IsSqlServer) await db.Database.ExecuteSqlRawAsync(SqlServerWipe(plan.Wipe), ct);
            else
            {
                await db.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys = ON;", ct);
                foreach (var t in plan.Wipe)
                {
                    var delete = "DELETE FROM " + Quote(t.Name, '"') + ";";
                    await db.Database.ExecuteSqlRawAsync(delete, ct);
                }
            }
            await tx.CommitAsync(ct);
        }

        featureAccess.InvalidateAll();

        string? movedTo = null, warning = null;
        if (UploadsRoot is { } root && Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            var target = Path.Combine(Path.GetDirectoryName(root)!, $"{Path.GetFileName(root)}-before-reset-{stamp}");
            try
            {
                Directory.Move(root, target);
                Directory.CreateDirectory(root);
                movedTo = target;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Data reset: uploaded files could not be moved aside");
                warning = $"Uploaded files could not be moved aside ({ex.Message}). They are still in {root}.";
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Data reset: uploaded files could not be moved aside");
                warning = $"Uploaded files could not be moved aside ({ex.Message}). They are still in {root}.";
            }
        }

        // The audit log itself was emptied; this is its first entry.
        audit.Add("Platform.DataReset", "Platform", actorId.ToString(),
            $"Emptied {plan.Wipe.Count} tables. Backup: {backup ?? "none (test database)"}. Uploads: {movedTo ?? "nothing moved"}.");
        await db.SaveChangesAsync(ct);
        logger.LogWarning("All company data was reset by platform user {UserId}. Backup {Backup}; uploads moved to {Uploads}", actorId, backup, movedTo);

        return Result<DataResetOutcome>.Ok(new DataResetOutcome(plan.Wipe.Count, backup, movedTo, warning));
    }

    private static Result<DataResetOutcome> Fail(string message) => Result<DataResetOutcome>.Fail(message);

    private async Task<Domain.Platform.PlatformUser?> ActorAsync(CancellationToken ct) =>
        context.PrincipalType == PrincipalType.Platform && context.UserId is { } id
            ? await db.PlatformUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
            : null;

    private sealed record WipeTable(string Name, string? Schema);
    private sealed record WipePlan(IReadOnlyList<WipeTable> Wipe, string? Error);

    /// <summary>Works out the tables to empty from the data model and refuses if a kept table points at one of them.</summary>
    private WipePlan BuildPlan()
    {
        var tables = db.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null && e.GetViewName() is null)
            .Select(e => new WipeTable(e.GetTableName()!, e.GetSchema()))
            .DistinctBy(t => (t.Schema, t.Name))
            .ToList();

        var missing = DataResetRules.KeptTables.Where(k => k != "__EFMigrationsHistory" && tables.All(t => !t.Name.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count > 0)
            return new WipePlan([], $"Reset stopped: the list of tables to keep no longer matches the data model ({string.Join(", ", missing)}). Nothing was deleted.");

        var wipe = tables.Where(t => !DataResetRules.KeptTables.Contains(t.Name)).ToList();
        var wiped = wipe.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dangling = db.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is { } n && DataResetRules.KeptTables.Contains(n))
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.GetTableName() is { } p && wiped.Contains(p))
            .Select(fk => $"{fk.DeclaringEntityType.GetTableName()} → {fk.PrincipalEntityType.GetTableName()}")
            .ToList();
        if (dangling.Count > 0)
            return new WipePlan([], $"Reset stopped: kept tables refer to company data ({string.Join(", ", dangling)}). Nothing was deleted.");

        return new WipePlan(wipe, null);
    }

    private static string Quote(string name, char open) =>
        open == '[' ? $"[{name.Replace("]", "]]")}]" : $"\"{name.Replace("\"", "\"\"")}\"";

    private static string Qualified(WipeTable t) =>
        t.Schema is null ? Quote(t.Name, '[') : $"{Quote(t.Schema, '[')}.{Quote(t.Name, '[')}";

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";

    /// <summary>
    /// One batch: switch off constraints, empty, switch them back on with validation, then restart numbering at 1 for
    /// tables that ever held rows (a table that never held a row already starts at 1; reseeding it would start it at 0).
    /// </summary>
    private static string SqlServerWipe(IReadOnlyList<WipeTable> wipe)
    {
        var sb = new StringBuilder("SET XACT_ABORT ON;\n");
        foreach (var t in wipe) sb.Append("ALTER TABLE ").Append(Qualified(t)).Append(" NOCHECK CONSTRAINT ALL;\n");
        foreach (var t in wipe) sb.Append("DELETE FROM ").Append(Qualified(t)).Append(";\n");
        foreach (var t in wipe) sb.Append("ALTER TABLE ").Append(Qualified(t)).Append(" WITH CHECK CHECK CONSTRAINT ALL;\n");
        foreach (var t in wipe)
        {
            var q = Literal(Qualified(t));
            sb.Append("IF EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID(").Append(q)
              .Append(") AND last_value IS NOT NULL) DBCC CHECKIDENT (").Append(q).Append(", RESEED, 0) WITH NO_INFOMSGS;\n");
        }
        return sb.ToString();
    }

    /// <summary>COPY_ONLY backup with checksums, then RESTORE VERIFYONLY. Returns the backup file path.</summary>
    private async Task<string> BackupAsync(string stamp, CancellationToken ct)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @db sysname = DB_NAME();
            DECLARE @dir nvarchar(4000) = COALESCE(NULLIF(@folder, N''), CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath')));
            IF @dir IS NULL THROW 50010, 'SQL Server did not report a backup folder; set Platform:DataResetBackupFolder.', 1;
            IF RIGHT(@dir, 1) NOT IN (N'\', N'/') SET @dir = @dir + N'\';
            DECLARE @file nvarchar(4000) = @dir + @db + N'-before-reset-' + @stamp + N'.bak';
            DECLARE @cmd nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = @f WITH COPY_ONLY, CHECKSUM, INIT, NAME = N''Before Control Center data reset'';'
                + N' RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM;';
            EXEC sp_executesql @cmd, N'@f nvarchar(4000)', @f = @file;
            SELECT @file;
            """;
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = CommandTimeoutSeconds;
            AddParameter(cmd, "@folder", config[DataResetRules.BackupFolderSetting] ?? string.Empty);
            AddParameter(cmd, "@stamp", stamp);
            var file = await cmd.ExecuteScalarAsync(ct) as string;
            return string.IsNullOrEmpty(file) ? throw new InvalidOperationException("SQL Server did not report the backup file.") : file;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.String;
        p.Size = 4000;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
