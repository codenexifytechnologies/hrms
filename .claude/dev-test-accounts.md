# Local development test accounts

Throwaway accounts created in the **local development database only** while verifying features in the browser.
Never reuse these values anywhere else.

## 2026-11-13: database wiped

At the user's request all company data was deleted from the local database `CodenexifyHRMS`. Kept: the Control
Center owner `owner@codenexify.local` (password set by the user), plans and prices, modules, permissions,
statutory rates and Control Center settings. The companies `qawall` and `demo-industries` and all their logins no
longer exist; register a new company at `/register` to test again.

To undo: restore the backup
`C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup\CodenexifyHRMS-before-wipe-20261113-141236.bak`
and move `src\Codenexify.HRMS.Web\App_Data\uploads-before-wipe-20261113-141236` back to `App_Data\uploads`.

The same reset is now built into the Control Center: System & maintenance → Danger zone → Reset all data
(`/superadmin/system/reset`). It is switched on only in `appsettings.Development.json`.

## Current test accounts

| Created | Purpose | Company code | Login email | Password |
|---|---|---|---|---|
| 2026-11-14 | Perks & benefits screen check (Foundation, trial). Has one plan, "Group health insurance". | `benefitsqa` | `admin@benefitsqa.test` | `Qa-71821358eb!9` |
| 2026-11-14 | Employee "Priya Tester" (EMP0001) in the same company, enrolled in the health plan with a spouse | `benefitsqa` | `priya@benefitsqa.test` | `Qb-71821358eb!9` |
| 2026-11-14 | Goals & reviews check (Growth plan, trial). Company goal "Grow revenue by 30%", review cycle "H2 2026 review" (active) | `growthqa` | `admin@growthqa.test` | `Qa-71821358eb!9` |
| 2026-11-14 | Manager "Mira Manager" (EMP0001, reviewed by HR); has not signed in yet, so the first sign-in asks for a new password | `growthqa` | `mira@growthqa.test` | `Qa-71821358eb!9` |
| 2026-11-14 | Employee "Ravi Report" (EMP0002, reports to Mira); self-review submitted, gave Mira feedback and a "Customer first" shout-out, has a fortnightly 1:1 with her, rated himself Advanced in Excel; on an active improvement plan (test data) | `growthqa` | `ravi@growthqa.test` | `Qb-71821358eb!9` |

Other growthqa test data (2026-11-14): department "Sales"; headcount plan "FY 2026-27" (approved) with one "Account
executive" position; employee "Neha Newhire" (EMP0003, Sales, joins 01 Dec 2026, no login) linked to it, who completed
pre-boarding through the welcome link sent to `neha.personal@example.test` (made-up address; test PAN and bank values).

## Previous accounts (deleted in the wipe)

| Created | Purpose | Company code | Login email |
|---|---|---|---|
| 2026-11-12 | Phase 4 self-service verification (trial org, later moved to Strength) | `qawall` | `admin@qawall.test` |
| 2026-11-12 | Phase 4 — employee "Ravi Kumar" in the same org | `qawall` | `ravi@qawall.test` |
