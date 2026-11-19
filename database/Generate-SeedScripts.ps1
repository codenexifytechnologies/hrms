<#
.SYNOPSIS
    Exports the platform setup and the Control Center (Super Admin) logins of the Codenexify HRMS database named in
    ConnectionStrings:Default (src/Codenexify.HRMS.Web/appsettings.json, as the app reads it) as
    idempotent INSERT scripts, so a new database (e.g. production) can be set up the same way:

        database/seed/100_PlatformSetup.sql  modules, permissions, plans (prices, limits, modules), statutory rules,
                                             policy pages, Control Center settings (without saved secrets)
        database/seed/110_SuperAdmins.sql    Control Center users with their password hashes and permissions

.DESCRIPTION
    Every row is inserted only when its natural key (code, slug, e-mail, ...) is missing, so the scripts can be run
    more than once and never overwrite changes made on the target. Run them after database/000_FullDatabase.sql.

    Not exported: company (tenant) data, invoices, audit logs, and secrets that are encrypted with this machine's
    Data Protection keys (SMTP/Razorpay/SSO/SMS secrets, two-step sign-in secrets). Re-enter those in the
    Control Center on the new server.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File database/Generate-SeedScripts.ps1

.EXAMPLE
    # Also add a brand-new Super Admin; you are asked for a one-time password (changed at first sign-in).
    powershell -ExecutionPolicy Bypass -File database/Generate-SeedScripts.ps1 -NewSuperAdminEmail admin@company.com -NewSuperAdminName "Company Admin"
#>
param(
    # Which appsettings.{Environment}.json may override appsettings.json (same rule as the app).
    [string]$Environment = $(if ($env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT } else { 'Development' }),
    [string]$OutputFolder = (Join-Path $PSScriptRoot 'seed'),
    [string]$NewSuperAdminEmail,
    [string]$NewSuperAdminName = 'Platform Owner',
    [Security.SecureString]$NewSuperAdminPassword,
    [switch]$SkipExistingSuperAdmins
)
$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture

# ------------------------------------------------------------------------------------------------ helpers

# The connection string comes from the web app's configuration, in the same order as at run time:
# appsettings.json, then appsettings.{Environment}.json, then the environment variable ConnectionStrings__Default.
function Read-ConnectionString([string]$file) {
    if (-not (Test-Path $file)) { return $null }
    # appsettings files may contain // comment lines (ASP.NET Core allows them); PowerShell's JSON reader does not.
    $json = (Get-Content $file -Encoding UTF8 | Where-Object { $_.TrimStart() -notlike '//*' }) -join "`n"
    $cfg = $json | ConvertFrom-Json
    if ($cfg.ConnectionStrings -and $cfg.ConnectionStrings.Default) { return [string]$cfg.ConnectionStrings.Default }
    return $null
}
$webFolder = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Codenexify.HRMS.Web'
$connectionString = Read-ConnectionString (Join-Path $webFolder 'appsettings.json')
$override = Read-ConnectionString (Join-Path $webFolder "appsettings.$Environment.json")
if ($override) { $connectionString = $override }
if ($env:ConnectionStrings__Default) { $connectionString = $env:ConnectionStrings__Default }
if (-not $connectionString) { throw "ConnectionStrings:Default is not set in $webFolder\appsettings.json." }
$Database = (New-Object Data.SqlClient.SqlConnectionStringBuilder $connectionString).InitialCatalog

$conn = New-Object Data.SqlClient.SqlConnection $connectionString
$conn.Open()

function Read-Rows([string]$sql) {
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql
    $r = $cmd.ExecuteReader()
    $types = @{}; for ($i = 0; $i -lt $r.FieldCount; $i++) { $types[$r.GetName($i)] = $r.GetDataTypeName($i) }
    $rows = New-Object Collections.ArrayList
    while ($r.Read()) {
        $row = [ordered]@{}
        for ($i = 0; $i -lt $r.FieldCount; $i++) { $row[$r.GetName($i)] = if ($r.IsDBNull($i)) { $null } else { $r.GetValue($i) } }
        [void]$rows.Add($row)
    }
    $r.Close()
    return @{ Rows = $rows; Types = $types }
}

function Lit($value, [string]$type) {
    if ($null -eq $value) { return 'NULL' }
    switch -regex ($type) {
        '^(n?varchar|n?char|n?text)$' { return "N'" + ([string]$value).Replace("'", "''") + "'" }
        '^bit$' { return $(if ($value) { '1' } else { '0' }) }
        '^date$' { return "'" + ([datetime]$value).ToString('yyyy-MM-dd', $inv) + "'" }
        '^datetime2?$' { return "'" + ([datetime]$value).ToString('yyyy-MM-ddTHH:mm:ss.fffffff', $inv) + "'" }
        default { return ([IFormattable]$value).ToString($null, $inv) }
    }
}

function Where-Key($row, $types, [string[]]$keys, [hashtable]$exprs) {
    ($keys | ForEach-Object {
        $v = if ($exprs -and $exprs.ContainsKey($_)) { $exprs[$_] } else { Lit $row[$_] $types[$_] }
        if ($v -eq 'NULL') { "[$_] IS NULL" } else { "[$_] = $v" }
    }) -join ' AND '
}

# Emits "IF NOT EXISTS (key match) INSERT ..." for each row. $exprs replaces a column's value with a SQL expression
# (used to turn foreign keys into look-ups by natural key, so ids may differ between databases).
function Emit([string]$table, $data, [string[]]$keys, [string[]]$skip = @(), [scriptblock]$exprsFor = $null) {
    $out = New-Object Text.StringBuilder
    foreach ($row in $data.Rows) {
        $exprs = if ($exprsFor) { & $exprsFor $row } else { @{} }
        $cols = @($row.Keys | Where-Object { $skip -notcontains $_ })
        $vals = $cols | ForEach-Object { if ($exprs.ContainsKey($_)) { $exprs[$_] } else { Lit $row[$_] $data.Types[$_] } }
        [void]$out.AppendLine("IF NOT EXISTS (SELECT 1 FROM [$table] WHERE $(Where-Key $row $data.Types $keys $exprs))")
        [void]$out.AppendLine("    INSERT INTO [$table] ($(($cols | ForEach-Object { "[$_]" }) -join ', ')) VALUES ($($vals -join ', '));")
    }
    return $out.ToString()
}

function Section([string]$title, $data) { "`r`n-- ---------------------------------------------------------------- $title ($($data.Rows.Count))`r`n" }

# ASP.NET Core Identity PasswordHasher V3 format (what the app uses): PBKDF2-HMAC-SHA512, 100,000 iterations,
# 16-byte salt, 32-byte subkey; header = 0x01 | prf | iterations | salt length (big-endian).
function New-PasswordHash([Security.SecureString]$secure) {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    try {
        $salt = New-Object byte[] 16; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
        $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes(([Text.Encoding]::UTF8.GetBytes($plain)), $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA512)
        $subkey = $kdf.GetBytes(32)
        $be = { param([uint32]$n) [byte[]]@((($n -shr 24) -band 255), (($n -shr 16) -band 255), (($n -shr 8) -band 255), ($n -band 255)) }
        $bytes = [byte[]](@(1) + (& $be 2) + (& $be 100000) + (& $be 16) + $salt + $subkey)
        return [Convert]::ToBase64String($bytes)
    } finally { $plain = $null }
}

$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
$latest = (Read-Rows 'SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC').Rows[0].MigrationId
$guard = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL OR NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'$latest')
    THROW 50000, 'Create the schema first: run database/000_FullDatabase.sql (it must include migration $latest).', 1;
BEGIN TRANSACTION;
"@
New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)

# ------------------------------------------------------------------------------------------------ platform setup

$features = Read-Rows 'SELECT * FROM Features ORDER BY SortOrder, Id'
$permissions = Read-Rows 'SELECT * FROM Permissions ORDER BY Id'
$plans = Read-Rows 'SELECT p.*, parent.Code AS ParentCode FROM SubscriptionPlans p LEFT JOIN SubscriptionPlans parent ON parent.Id = p.ParentPlanId ORDER BY p.Id'
$planFeatures = Read-Rows 'SELECT p.Code AS PlanCode, f.Code AS FeatureCode FROM PlanFeatures pf JOIN SubscriptionPlans p ON p.Id = pf.PlanId JOIN Features f ON f.Id = pf.FeatureId ORDER BY p.Id, f.SortOrder'
$rateSets = Read-Rows 'SELECT * FROM StatutoryRateSets ORDER BY EffectiveFrom'
$taxRules = Read-Rows 'SELECT * FROM IncomeTaxRules ORDER BY FinancialYear, Regime'
$taxSlabs = Read-Rows 'SELECT s.*, r.FinancialYear, r.Regime FROM IncomeTaxSlabs s JOIN IncomeTaxRules r ON r.Id = s.IncomeTaxRuleId ORDER BY r.FinancialYear, r.Regime, s.FromIncome'
$surcharges = Read-Rows 'SELECT s.*, r.FinancialYear, r.Regime FROM IncomeTaxSurcharges s JOIN IncomeTaxRules r ON r.Id = s.IncomeTaxRuleId ORDER BY r.FinancialYear, r.Regime, s.AboveIncome'
$ptSlabs = Read-Rows 'SELECT * FROM ProfessionalTaxSlabs ORDER BY State, EffectiveFrom, MinMonthlyGross'
$lwf = Read-Rows 'SELECT * FROM LabourWelfareFundRules ORDER BY State, EffectiveFrom'
$legal = Read-Rows 'SELECT * FROM LegalPages ORDER BY SortOrder'
$settings = Read-Rows 'SELECT TOP 1 * FROM PlatformSettings ORDER BY Id'
$announcements = Read-Rows 'SELECT a.*, p.Code AS PlanCode FROM PlatformAnnouncements a LEFT JOIN SubscriptionPlans p ON p.Id = a.PlanId ORDER BY a.Id'

$rule = { param($row) "(SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = $($row.FinancialYear) AND [Regime] = N'$($row.Regime)')" }
$planRef = { param($code) if ($null -eq $code) { 'NULL' } else { "(SELECT [Id] FROM [SubscriptionPlans] WHERE [Code] = N'$($code.Replace("'", "''"))')" } }

$sb = New-Object Text.StringBuilder
[void]$sb.AppendLine(@"
/* =====================================================================================================================
   Codenexify HRMS - platform setup (starting data)
   Exported $stamp from [$Database] by database/Generate-SeedScripts.ps1. Do not edit by hand; regenerate instead.

   Contains: modules (features), permissions, subscription plans with prices/limits and their modules, statutory
   rules (PF/ESI/gratuity, income tax, professional tax, LWF), public policy pages, Control Center settings and
   announcements. No company data.

   RUN ORDER on a new database:
     1. database/000_FullDatabase.sql
     2. database/seed/100_PlatformSetup.sql   (this file)
     3. database/seed/110_SuperAdmins.sql
     4. Start the application.
   e.g.  sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\seed\100_PlatformSetup.sql

   Safe to run again: each row is inserted only if its key is missing; existing rows are never changed.
   Saved secrets are NOT included (they are encrypted with the source server's keys). SMTP, payment gateway and SMS
   are switched off here: re-enter their passwords/keys in Control Center -> Billing & email settings, then switch
   them on.
   ===================================================================================================================== */
"@)
[void]$sb.AppendLine($guard)

[void]$sb.Append((Section 'Modules (features)' $features)); [void]$sb.Append((Emit 'Features' $features @('Code') @('Id')))
[void]$sb.Append((Section 'Permissions' $permissions)); [void]$sb.Append((Emit 'Permissions' $permissions @('Code') @('Id')))
[void]$sb.Append((Section 'Subscription plans' $plans))
[void]$sb.Append((Emit 'SubscriptionPlans' $plans @('Code') @('Id', 'RowVersion', 'ParentCode') { param($r) @{ ParentPlanId = (& $planRef $r.ParentCode) } }))
[void]$sb.Append((Section 'Modules in each plan' $planFeatures))
foreach ($pf in $planFeatures.Rows) {
    $p = $pf.PlanCode.Replace("'", "''"); $f = $pf.FeatureCode.Replace("'", "''")
    [void]$sb.AppendLine("INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'$p' AND f.[Code] = N'$f' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);")
}
[void]$sb.Append((Section 'PF / ESI / gratuity rates' $rateSets)); [void]$sb.Append((Emit 'StatutoryRateSets' $rateSets @('EffectiveFrom') @('Id')))
[void]$sb.Append((Section 'Income tax rules' $taxRules)); [void]$sb.Append((Emit 'IncomeTaxRules' $taxRules @('FinancialYear', 'Regime') @('Id')))
[void]$sb.Append((Section 'Income tax slabs' $taxSlabs))
[void]$sb.Append((Emit 'IncomeTaxSlabs' $taxSlabs @('IncomeTaxRuleId', 'FromIncome') @('Id', 'FinancialYear', 'Regime') { param($r) @{ IncomeTaxRuleId = (& $rule $r) } }))
[void]$sb.Append((Section 'Income tax surcharges' $surcharges))
[void]$sb.Append((Emit 'IncomeTaxSurcharges' $surcharges @('IncomeTaxRuleId', 'AboveIncome') @('Id', 'FinancialYear', 'Regime') { param($r) @{ IncomeTaxRuleId = (& $rule $r) } }))
[void]$sb.Append((Section 'Professional tax slabs' $ptSlabs)); [void]$sb.Append((Emit 'ProfessionalTaxSlabs' $ptSlabs @('State', 'EffectiveFrom', 'Gender', 'MinMonthlyGross') @('Id')))
[void]$sb.Append((Section 'Labour welfare fund' $lwf)); [void]$sb.Append((Emit 'LabourWelfareFundRules' $lwf @('State', 'EffectiveFrom') @('Id')))
[void]$sb.Append((Section 'Policy pages' $legal)); [void]$sb.Append((Emit 'LegalPages' $legal @('Slug') @('Id')))

[void]$sb.Append((Section 'Control Center settings (secrets removed)' $settings))
foreach ($row in $settings.Rows) {
    $off = @{ SmtpEnabled = '0'; RazorpayEnabled = '0'; SmsEnabled = '0' }
    foreach ($k in @($row.Keys)) { if ($k -like '*Protected') { $off[$k] = 'NULL' } }
    $cols = @($row.Keys | Where-Object { $_ -ne 'Id' })
    $vals = $cols | ForEach-Object { if ($off.ContainsKey($_)) { $off[$_] } else { Lit $row[$_] $settings.Types[$_] } }
    [void]$sb.AppendLine("IF NOT EXISTS (SELECT 1 FROM [PlatformSettings])")
    [void]$sb.AppendLine("    INSERT INTO [PlatformSettings] ($(($cols | ForEach-Object { "[$_]" }) -join ', ')) VALUES ($($vals -join ', '));")
}
[void]$sb.Append((Section 'Announcements' $announcements))
[void]$sb.Append((Emit 'PlatformAnnouncements' $announcements @('Title', 'StartsAtUtc') @('Id', 'PlanCode') { param($r) @{ PlanId = (& $planRef $r.PlanCode) } }))

[void]$sb.AppendLine("`r`nCOMMIT TRANSACTION;")
[void]$sb.AppendLine("PRINT 'Platform setup applied.';")
$setupPath = Join-Path $OutputFolder '100_PlatformSetup.sql'
[IO.File]::WriteAllText($setupPath, $sb.ToString(), $utf8)

# ------------------------------------------------------------------------------------------------ Super Admins

$users = if ($SkipExistingSuperAdmins) { @{ Rows = @(); Types = @{} } } else { Read-Rows 'SELECT * FROM PlatformUsers WHERE IsActive = 1 ORDER BY Id' }
$userPerms = if ($SkipExistingSuperAdmins) { @{ Rows = @() } } else { Read-Rows 'SELECT u.Email, p.Permission FROM PlatformUserPermissions p JOIN PlatformUsers u ON u.Id = p.PlatformUserId WHERE u.IsActive = 1 ORDER BY u.Email, p.Permission' }

$sa = New-Object Text.StringBuilder
[void]$sa.AppendLine(@"
/* =====================================================================================================================
   Codenexify HRMS - Control Center (Super Admin) logins
   Exported $stamp from [$Database] by database/Generate-SeedScripts.ps1.

   !! SENSITIVE: contains password hashes. Keep it out of source control and shared folders; delete it after use. !!

   Each active Control Center user keeps their current password (the hash is copied; it does not depend on the
   server). Two-step sign-in is switched off because its secret is encrypted with the source server's keys: each
   user turns it on again after signing in. Users created with -NewSuperAdminEmail must change their one-time
   password at first sign-in.

   Run after database/seed/100_PlatformSetup.sql:
       sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\seed\110_SuperAdmins.sql
   Safe to run again: users whose e-mail already exists are left unchanged.
   ===================================================================================================================== */
"@)
[void]$sa.AppendLine($guard)
$reset = { param($r) @{
    TwoFactorEnabled = '0'; TwoFactorSecret = 'NULL'; FailedLoginCount = '0'; LockoutEndUtc = 'NULL'; LastLoginAtUtc = 'NULL'; LastLoginIp = 'NULL'
    SecurityStamp = "REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')" } }
[void]$sa.Append((Section 'Control Center users' $users))
[void]$sa.Append((Emit 'PlatformUsers' $users @('Email') @('Id') $reset))

$newCount = 0
if ($NewSuperAdminEmail) {
    if (-not $NewSuperAdminPassword) { $NewSuperAdminPassword = Read-Host "One-time password for $NewSuperAdminEmail (min 10 characters)" -AsSecureString }
    if ($NewSuperAdminPassword.Length -lt 10) { throw 'The one-time password must be at least 10 characters.' }
    $email = $NewSuperAdminEmail.Trim().ToLowerInvariant().Replace("'", "''")
    $name = $NewSuperAdminName.Trim().Replace("'", "''")
    $hash = New-PasswordHash $NewSuperAdminPassword
    $now = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffffff', $inv)
    [void]$sa.AppendLine("`r`n-- ---------------------------------------------------------------- New Super Admin (must change the password at first sign-in)")
    [void]$sa.AppendLine("IF NOT EXISTS (SELECT 1 FROM [PlatformUsers] WHERE [Email] = N'$email')")
    [void]$sa.AppendLine("    INSERT INTO [PlatformUsers] ([Email], [FullName], [PasswordHash], [Role], [IsActive], [MustChangePassword], [TwoFactorEnabled], [FailedLoginCount], [SecurityStamp], [CreatedAtUtc], [CreatedBy])")
    [void]$sa.AppendLine("    VALUES (N'$email', N'$name', N'$hash', N'SuperAdmin', 1, 1, 0, 0, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), '$now', N'seed-script');")
    $newCount = 1
}

[void]$sa.Append((Section 'Control Center permissions (for non-Super Admin roles)' $userPerms))
foreach ($p in $userPerms.Rows) {
    $e = $p.Email.Replace("'", "''"); $perm = $p.Permission.Replace("'", "''")
    [void]$sa.AppendLine("INSERT INTO [PlatformUserPermissions] ([PlatformUserId], [Permission]) SELECT u.[Id], N'$perm' FROM [PlatformUsers] u WHERE u.[Email] = N'$e' AND NOT EXISTS (SELECT 1 FROM [PlatformUserPermissions] x WHERE x.[PlatformUserId] = u.[Id] AND x.[Permission] = N'$perm');")
}
[void]$sa.AppendLine("`r`nCOMMIT TRANSACTION;")
[void]$sa.AppendLine("PRINT 'Control Center users applied.';")
$adminPath = Join-Path $OutputFolder '110_SuperAdmins.sql'
[IO.File]::WriteAllText($adminPath, $sa.ToString(), $utf8)
$conn.Close()

Write-Host "Wrote $setupPath"
Write-Host ("  {0} modules, {1} permissions, {2} plans, {3} plan-module links, {4} policy pages, statutory rules" -f $features.Rows.Count, $permissions.Rows.Count, $plans.Rows.Count, $planFeatures.Rows.Count, $legal.Rows.Count)
Write-Host "Wrote $adminPath"
Write-Host ("  {0} existing Control Center user(s), {1} new" -f $users.Rows.Count, $newCount)
Write-Host "110_SuperAdmins.sql contains password hashes: keep it private and delete it after use."
