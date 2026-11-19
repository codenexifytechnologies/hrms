# Codenexify HRMS — Setup & Deployment

## Prerequisites
| | Version |
|---|---|
| .NET SDK | 9.0 (solution targets `net9.0`; see *Runtime support* below) |
| SQL Server | 2017+ (Express works for development) |
| IIS | 10 with the **ASP.NET Core Hosting Bundle** matching the target runtime |

## Local development
```bash
dotnet tool restore
dotnet build
dotnet run --project src/Codenexify.HRMS.Web --urls http://localhost:5080
```
On start the app applies migrations (unless `Database__AutoMigrate=false`) and seeds the feature catalog, permissions, the four default plans and the
first Super Admin (from `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword`; Development values are in
`appsettings.Development.json`). The seeded admin must change the password at first sign-in.

| URL | Portal |
|---|---|
| `/` | Public site with plans |
| `/register` | Self-service company registration (trial) |
| `/login` | Organization sign-in (company code + email) |
| `/org` | Organization Admin portal |
| `/me` | Personal area for every organization user |
| `/superadmin` | Main Super Admin Control Center |
| `/api/v1/auth/token`, `/api/v1/me` | Mobile / integration API (JWT bearer) |

Run tests: `dotnet test` (SQLite in-memory; includes HTTP-level portal-separation tests).

## Configuration (environment variables override `appsettings.json`)
| Key | Purpose |
|---|---|
| `ConnectionStrings__Default` | SQL Server connection string. Read only from configuration: `appsettings.json` → `appsettings.{Environment}.json` → this environment variable (nothing is hard-coded; the app, `dotnet ef` and the scripts in `/database` all use it and stop with a clear error if it is missing). `appsettings.json` has a Windows-authentication string and, commented out, a SQL-login version (`User Id=…;Password=…`); on live servers set the variable instead of writing the password into the file. |
| `Jwt__Key` | **Required.** ≥ 32 random characters for API token signing |
| `Seed__SuperAdminEmail`, `Seed__SuperAdminPassword` | First Super Admin (used only when no platform user exists) |
| `DataProtection__KeysPath` | Folder for Data Protection keys (cookies, encrypted TOTP secrets). Must be persistent and backed up. |
| `Storage__LocalRoot` | Folder for uploaded employee documents (default `App_Data/uploads` under the site). Keep it outside the web root, back it up with the database, and give the app-pool identity write access. |
| `Database__AutoMigrate` | `true` applies migrations at start; set `false` if DBAs deploy scripts. With `false` the app refuses to start while the database is missing a migration, naming the missing ones. Starting data is seeded either way. |
| `BackgroundJobs__Enabled` | Subscription lifecycle job (hourly) |
| `Security__MaxFailedLogins`, `Security__LockoutMinutes` | Lockout policy |
| `Platform__ProductName`, `Platform__SupportEmail` | Branding and support contact |
| `Platform__AllowDataReset` | Allows Control Center → System & maintenance → **Reset all data**. `false` in `appsettings.json`, `true` only in `appsettings.Development.json`. Leave it off on live servers. |
| `Platform__DataResetBackupFolder` | Optional folder (on the SQL Server machine) for the automatic backup taken before a reset; default is SQL Server's own backup folder |

Never commit production secrets; use environment variables in IIS (`web.config` `<environmentVariables>`),
Plesk *ASP.NET Core settings*, or a secret store.

## Database
* Migrations live in `src/Codenexify.HRMS.Infrastructure/Persistence/Migrations`.
* New migration: `dotnet ef migrations add <Name> -p src/Codenexify.HRMS.Infrastructure -s src/Codenexify.HRMS.Infrastructure -o Persistence/Migrations`
* **Full database script:** `database/000_FullDatabase.sql` builds the whole schema (every module) in one run on an empty database, and is safe to run on an existing one (applied migrations are skipped). Steps: create an empty database, run the file (SSMS, or `sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\000_FullDatabase.sql`), then start the app: it seeds the feature catalog, permissions, plans, statutory rates, policy drafts and the first Super Admin. Regenerate it after every new migration with `powershell -ExecutionPolicy Bypass -File database/Generate-FullDatabaseScript.ps1`.
* **Starting data and Super Admin logins as SQL** (`database/seed/`), generated from a database with
  `powershell -ExecutionPolicy Bypass -File database/Generate-SeedScripts.ps1` (it connects with
  `ConnectionStrings:Default` from `src/Codenexify.HRMS.Web/appsettings.json`, like the app; `-Environment` picks the
  `appsettings.{Environment}.json` override, default Development):
  * `100_PlatformSetup.sql`: modules, permissions, plans with prices, limits and modules, statutory rules (PF/ESI/
    gratuity, income tax, PT, LWF), policy pages, Control Center settings and announcements. Saved secrets are left
    out (they are encrypted with the source server's Data Protection keys), so SMTP, Razorpay and SMS arrive switched
    off: re-enter their passwords/keys in the Control Center and switch them on.
  * `110_SuperAdmins.sql`: active Control Center users with their current password hashes (same passwords work) and
    permissions. Two-step sign-in is switched off (its secret can't move servers); users turn it on again.
    **Contains password hashes: keep it private, don't commit it, delete it after use.** Add a new Super Admin with
    `-NewSuperAdminEmail you@company.com -NewSuperAdminName "Name"` (asks for a one-time password, which must be changed
    at first sign-in); `-SkipExistingSuperAdmins` leaves out the exported users.
  * New server: `000_FullDatabase.sql`, then `seed/100_PlatformSetup.sql`, then `seed/110_SuperAdmins.sql`, then start
    the app. Each row is inserted only if its code/slug/e-mail is missing, so the scripts can be re-run and never
    overwrite changes. Without these scripts the app still seeds default starting data and the Super Admin from
    `Seed__SuperAdminEmail` / `Seed__SuperAdminPassword` on first start.
* Idempotent script per release (to upgrade step by step): `dotnet ef migrations script <Previous> <New> --idempotent -p src/Codenexify.HRMS.Infrastructure -s src/Codenexify.HRMS.Infrastructure -o database/<file>.sql`
  (scripts: `database/001_InitialPlatform.sql`, `database/002_CoreHr.sql`, `database/003_TimeAttendance.sql`, `database/004_Payroll.sql`, `database/005_Billing.sql`, `database/006_Support.sql`, `database/007_Engagement.sql`, `database/008_Letters.sql`, `database/009_PayrollExtras.sql`, `database/010_Strength.sql`, `database/011_StrengthPass2.sql`, `database/012_BrandDefaults.sql`, `database/013_PasswordReset.sql`, `database/014_LegalPages.sql`, `database/015_PerksAndBenefits.sql`, `database/016_GoalsAndReviews.sql`, `database/017_FeedbackAndOneOnOnes.sql`, `database/018_ValuesAndSkills.sql`, `database/019_PromotionsAndPips.sql`, `database/020_CompensationAndCalibration.sql`, `database/021_WorkflowAndReports.sql`, `database/022_HeadcountAndPreboarding.sql`).
* **Brand:** logo files live in `wwwroot/img/brand` (PNG/WebP originals plus SVG marks); the colour tokens
  (`--cnx-navy` #0B1F3A, `--cnx-blue` #1F5EFF, `--cnx-blue-light` #5B8CFF for dark mode) are at the top of `wwwroot/css/app.css`.
  Client portals still use each organization's own brand colour (Company profile), defaulting to Codenexify blue.
* **Single sign-on:** register a Google OAuth web client and a Microsoft Entra multi-tenant app (“accounts in any
  organizational directory”), with redirect URIs `https://<host>/sso/google/callback` and `https://<host>/sso/microsoft/callback`,
  then enter the client ids and secrets in Control Center → Billing → Settings. No restart needed.
* **Mobile OTP:** needs an MSG91 account with a DLT-registered sender and an OTP flow template; enter the auth key,
  template id and the template's variable name in the same settings page.
* **Selfie clock-in** uses the browser camera, which (like GPS) requires HTTPS in production.
* **GPS attendance** needs HTTPS in production (browsers only share location with secure pages). Employees are asked
  for location permission when they clock in; nothing is tracked between punches.
* **Statutory files**: enter the TAN and PF establishment code under Payroll → Settings before issuing Form 16 or
  uploading ECR files. Form 16 Part B must be issued together with Part A from TRACES; the 24Q annexure is input
  for the Return Preparation Utility (challan details are added there).
* **PDF output** (letters, payslips, invoices) uses PDFsharp/MigraDoc **6.1.1, MIT licence — do not upgrade**: from
  6.5 the package is AGPL-3.0, which would oblige you to publish the application source. Fonts: Arial from the
  Windows fonts folder on IIS/Plesk; on Linux install DejaVu Sans (`fonts-dejavu-core`).
* **Letter templates**: every organization starts with offer, appointment, confirmation, experience and relieving
  templates in general wording. Ask customers to review them with their legal/HR advisor before use.
* **Notification emails:** set `Platform__PublicUrl` (e.g. `https://hr.example.com`) so emailed notifications link
  back into the portal. Copies are sent every minute when SMTP is configured, the organization's plan includes
  the notification engine, and the user has not opted out.
* **Support desk:** set the support inbox in Control Center → System & maintenance. Maintenance mode returns 503
  to organization portals, self-service and the API (checked every 15 s); the Control Center and `/webhooks/*`
  stay available so you can switch it off and payments are not lost.
* **Statutory rules are data.** On first start the platform tables are seeded with starting values (PF, ESI,
  gratuity, PT for Maharashtra/Karnataka/Gujarat/Telangana, LWF for Maharashtra/Karnataka, income tax FY 2025-26
  and 2026-27). They are marked *unverified*: before running real payroll, a Super Admin must check every value
  against the current EPFO/ESIC notifications, state PT/LWF acts and the Finance Act, correct them in
  Control Center → Statutory rules, and mark them verified. Add other states as needed.
* Background jobs (`BackgroundJobs__Enabled`): subscription lifecycle, time & attendance (hourly: finalizes
  yesterday's attendance and refreshes leave accruals per organization) and billing (every 6 hours: renewal
  invoices, payment reminders, trial-ending emails).
* **Billing go-live** (Control Center → Billing & email settings): enter the company GSTIN, address and bank
  details; confirm the SAC code and GST rate with your tax advisor; enter Razorpay *live* keys and a webhook
  secret, and in the Razorpay dashboard point a webhook at `https://<your-domain>/webhooks/razorpay` for
  `payment.captured` and `payment.failed`; enter SMTP settings. The webhook URL must be reachable over HTTPS
  and must not sit behind the `/superadmin` IP restriction. On IIS set the app pool to
  *AlwaysRunning* with preload enabled so jobs keep running without traffic.
* **Legal pages** (`/privacy`, `/terms`, `/refund-policy`, `/contact`; Razorpay and the DPDP Act expect these):
  first start creates starter drafts. Before going live: enter the legal name, address, GSTIN, phone and billing
  email (Billing & email settings), the support email (System & maintenance) and the Grievance Officer and court
  city (Control Center → Legal pages); have a lawyer review each page, edit it there and tick *Reviewed by our
  lawyer*. Any detail still missing prints as "not yet provided" and is listed at the top of the Legal pages screen.
  For material changes tick *new version*; new sign-ups record the versions they accepted.
* **Forgot password** (`/forgot-password` for companies, `/superadmin/account/forgot-password` for staff) emails a
  one-time link valid for 30 minutes. It needs SMTP (Control Center → Billing & email settings) and
  `Platform__PublicUrl` set to the site's HTTPS address: outside Development no link is sent without it, so a forged
  Host header can't redirect links to another site. Without SMTP, an HR administrator (or another Super Admin) can
  still set a new password for the user.
* **Pre-boarding links** (`/welcome?token=…`) need the same `Platform__PublicUrl`. Without SMTP, HR can copy the link
  shown once after sending and share it with the new hire directly. The page allows 40 requests a minute per IP
  (policy `preboarding`).
* **Reset all data** (Control Center → System & maintenance → Danger zone; Super Admin only, and only when
  `Platform__AllowDataReset` is `true`): deletes every company with its users, employees, records, invoices,
  support tickets, audit log and login history. Control Center users, plans, modules, permissions, statutory
  rates, Control Center settings and announcements are kept. The Super Admin types `DELETE ALL COMPANY DATA`,
  re-enters their password (and authenticator code if two-step sign-in is on). A COPY_ONLY backup with checksums
  is taken and verified first (`<database>-before-reset-<yyyyMMdd-HHmmss>.bak`); if it fails nothing is deleted.
  The tables are emptied in one transaction, numbering restarts at 1, and the uploads folder is moved to
  `uploads-before-reset-<stamp>` beside it. The SQL Server login needs `BACKUP DATABASE` permission
  (`db_backupoperator`) and ALTER on the tables. To undo, restore the `.bak` file and move the uploads folder back.
* Upgrading an existing database also runs the seeder: it creates HR settings/exit checklists for older
  organizations and grants permissions introduced by the release to their system roles (once).
* The application login needs `db_datareader`, `db_datawriter` (+ `db_ddladmin` only if `AutoMigrate=true`). Seeding at start only reads and writes data, so it works with `AutoMigrate=false`.
* Backups: full daily + log backups every 15 min (FULL recovery model); test restores monthly.
  Back up the Data Protection key folder together with the database.

## IIS / Plesk deployment
1. `dotnet publish src/Codenexify.HRMS.Web -c Release -o publish`
2. Create the site / app pool (No Managed Code, Integrated pipeline), point it at `publish`.
3. Set environment variables (`ASPNETCORE_ENVIRONMENT=Production`, connection string, `Jwt__Key`,
   `DataProtection__KeysPath` to a folder outside the site that the app-pool identity can write).
4. Bind HTTPS (the app sends HSTS and secure-only cookies in Production).
5. Behind a reverse proxy / load balancer, forwarded headers are honoured (`X-Forwarded-For/Proto`).
6. Optionally restrict `/superadmin` by IP at IIS level in addition to per-user IP allow-lists.

## Runtime support
.NET 8 and .NET 9 reached end of support on 10 Nov 2026. Before production, move to .NET 10 (LTS): install the
.NET 10 SDK/Hosting Bundle, change `<TargetFramework>` to `net10.0` in each `.csproj` and update the
`Microsoft.*` / EF Core packages to 10.x, then rebuild and run the tests.
