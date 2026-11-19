<#
.SYNOPSIS
    Regenerates database/000_FullDatabase.sql: every migration from an empty database to the latest, in one
    idempotent script. Run it from the repository root after adding a migration:

        powershell -ExecutionPolicy Bypass -File database/Generate-FullDatabaseScript.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/Codenexify.HRMS.Infrastructure'
$target = Join-Path $PSScriptRoot '000_FullDatabase.sql'
$body = Join-Path ([IO.Path]::GetTempPath()) "cnx-full-$([Guid]::NewGuid().ToString('N')).sql"

Push-Location $root
try {
    dotnet ef migrations script --idempotent -p $project -s $project -c AppDbContext -o $body
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations script failed.' }

    $latest = (Select-String -Path $body -Pattern "VALUES \(N'(\d{14}_\w+)'" -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object | Select-Object -Last 1)
    $count = (Select-String -Path $body -Pattern "VALUES \(N'\d{14}_\w+'" -AllMatches | Measure-Object).Count

    $header = @"
/* =====================================================================================================================
   Codenexify HRMS - full database script (schema for every module, $count migrations, latest: $latest)
   Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm') by database/Generate-FullDatabaseScript.ps1. Do not edit by hand.

   USE IT FOR
     * A brand-new database: creates every table, index and key in one go (instead of running 001-0NN one by one).
     * An existing database: safe to run too. Each migration checks __EFMigrationsHistory and is skipped if applied,
       so only what is missing is added. Back up first, as with any schema change.

   HOW
     1. Create an empty database (SQL Server 2019+ / Azure SQL), e.g.:
            CREATE DATABASE CodenexifyHRMS COLLATE SQL_Latin1_General_CP1_CI_AS;
     2. Run this file against it (SSMS: open and Execute, or):
            sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\000_FullDatabase.sql
     3. Start the application. On start it fills in the starting data that this script does not contain:
        feature catalog, permissions, the default plans, statutory rates, legal page drafts and the first Super Admin
        (Seed__SuperAdminEmail / Seed__SuperAdminPassword). This happens even with Database__AutoMigrate=false;
        the app only needs db_datareader + db_datawriter for it.

   The script holds structure only: no company data, no passwords.
   ===================================================================================================================== */

"@
    $utf8 = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($target, $header + [IO.File]::ReadAllText($body, $utf8), $utf8)
    Write-Host "Wrote $target ($count migrations, latest $latest)"
}
finally {
    Remove-Item $body -ErrorAction SilentlyContinue
    Pop-Location
}
