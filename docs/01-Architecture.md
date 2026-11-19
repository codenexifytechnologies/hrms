# Codenexify HRMS — System Architecture

**Owner:** Codenexify Technologies Private Limited
**Product:** Multi-tenant HRMS & Employee Experience SaaS platform
**Stack:** ASP.NET Core (MVC + Web API) · C# · EF Core + Dapper · SQL Server · Bootstrap 5 / jQuery / Chart.js

---

## 1. High-level view

```
                          ┌────────────────────────────────────────────┐
   Browser / Mobile app → │  Codenexify.HRMS.Web  (single IIS/Plesk site) │
                          │                                            │
                          │  /superadmin/*  → Area "SuperAdmin"        │  scheme: PlatformCookie
                          │  /org/*         → Area "OrgAdmin"          │  scheme: TenantCookie
                          │  /me/*          → Area "Employee" (Ph. 2+) │  scheme: TenantCookie
                          │  /api/v1/*      → REST API controllers     │  scheme: JWT Bearer (tenant)
                          │  /, /login, /register → public site        │
                          └───────────────┬────────────────────────────┘
                                          │ DI
                ┌─────────────────────────┴──────────────────────────┐
                │ Codenexify.HRMS.Application                        │
                │  interfaces · DTOs · services · feature codes      │
                └─────────────────────────┬──────────────────────────┘
                                          │
                ┌─────────────────────────┴──────────────────────────┐
                │ Codenexify.HRMS.Infrastructure                     │
                │  AppDbContext (global tenant + soft-delete filters) │
                │  audit capture · password hashing · TOTP · jobs     │
                │  Dapper connection factory (reporting queries)      │
                └─────────────────────────┬──────────────────────────┘
                                          │
                                   SQL Server (shared DB,
                                   OrganizationId isolation)
```

Clean Architecture dependency rule: **Domain ← Application ← Infrastructure ← Web**. Domain has no
external dependencies. Application depends only on Domain and EF Core abstractions (`DbSet`/`IQueryable`).

| Project | Responsibility |
|---|---|
| `Codenexify.HRMS.Domain` | Entities, enums, base types (`BaseEntity`, `TenantEntity`, `ISoftDelete`), domain rules (e.g. subscription state). |
| `Codenexify.HRMS.Application` | Use-case services (organizations, plans, subscriptions, feature access, platform users, tenant users/roles), `ICurrentContext`, DTOs, feature & permission catalogs. |
| `Codenexify.HRMS.Infrastructure` | `AppDbContext`, EF configurations, migrations, seeding, audit trail, password hasher, TOTP, background jobs, Dapper. |
| `Codenexify.HRMS.Web` | MVC Areas (SuperAdmin, OrgAdmin), public pages (login/register), API controllers, authentication/authorization setup, filters, UI. |
| `Codenexify.HRMS.Tests` | Unit + integration tests (tenant isolation, plan inheritance, subscription expiry, feature gating, auth separation). |

One deployable web host keeps IIS/Plesk hosting simple. The API controllers live in the same host behind a
separate JWT scheme so the mobile apps can use them; they can be split into their own host later without changing
Application/Infrastructure.

## 2. Two administration levels

| | Main Super Admin (platform owner) | Organization Admin (client company) |
|---|---|---|
| URL | `/superadmin` | `/org` (login at `/login`) |
| Users table | `PlatformUsers` (no `OrganizationId`) | `Users` (tenant-owned, has `OrganizationId`) |
| Auth scheme | `PlatformCookie` (cookie name `cnx.platform`, path `/superadmin`) | `TenantCookie` (cookie `cnx.tenant`) / JWT for API |
| Authorization | Policy `Platform` + granular platform permissions | Policy `Tenant` + tenant role permissions + feature gates |
| Data scope | All organizations (tenant filter bypassed **only** for platform principals) | Own organization only |

A tenant principal can never satisfy the `Platform` policy: the policy is bound to the `PlatformCookie`
authentication scheme and requires the `cnx:principal=platform` claim, which only the platform login issues.

## 3. Multi-tenancy

* **Model:** shared database, every tenant-owned table derives from `TenantEntity` (`OrganizationId` NOT NULL,
  FK → `Organizations`, indexed). Designed so an enterprise tenant can later be moved to a dedicated database
  (connection-string-per-tenant resolver behind `ITenantConnectionResolver`).
* **Resolution:** `ICurrentContext` reads `OrganizationId` **only from the authenticated principal's claims**
  (`cnx:org`). Route values, query strings, form fields and headers are never used to pick the tenant.
* **Central read filter:** `AppDbContext.OnModelCreating` adds a global query filter to every `TenantEntity`:
  `e => (ctx.IsPlatform || e.OrganizationId == ctx.OrganizationId) && !e.IsDeleted`.
  An unauthenticated request has `OrganizationId = null` and therefore sees no tenant rows.
* **Central write guard:** `SaveChanges` stamps `OrganizationId` on new tenant rows from the context and throws
  `TenantIsolationException` if code tries to insert/modify/delete a row belonging to another tenant.
* **Platform operations** that intentionally cross tenants live only in platform services and are only reachable
  through the `Platform` policy.
* **Tests** (`TenantIsolationTests`) prove tenant A cannot read, update or delete tenant B's rows.

## 4. Subscription & feature engine

* `Features` — catalog of switchable modules/features (code, name, tier, module group, global on/off).
* `SubscriptionPlans` — Foundation → Strength → Growth form an inheritance chain through `ParentPlanId`;
  a plan's effective features = its own `PlanFeatures` ∪ all ancestors'. Custom Enterprise plans pick any
  features. New plans need no code changes.
* `OrganizationSubscriptions` — plan, billing cycle, status (Trial, Active, PastDue, Paused, Cancelled, Expired),
  period dates, price, optional limit overrides. Exactly one current subscription per organization.
* `OrganizationFeatureOverrides` — per-organization add-ons (enable) or restrictions (disable), optional expiry.
* **Decision** (`FeatureAccessService.IsEnabledAsync(org, code)`):
  1. feature exists and is globally enabled, 2. organization status allows use (Active/Trial),
  3. current subscription is usable (not expired/paused/cancelled, trial not lapsed),
  4. an unexpired override wins; otherwise the plan's effective feature set decides.
* **Enforcement:** `[RequireFeature("CODE")]` MVC/API filter, navigation built from enabled features, limit
  checks (employees/branches) in the services that create those records.
* **Background job** `SubscriptionLifecycleJob` (hosted service, hourly) moves lapsed trials/subscriptions to
  `Expired` and the organization status accordingly.

## 5. Security

* Passwords: ASP.NET Core Identity `PasswordHasher` (PBKDF2-HMAC-SHA512, 100k iterations, salted).
* Lockout after configurable failed attempts; every login attempt recorded in `LoginAudits`.
* TOTP two-factor authentication (RFC 6238) for platform users; IP allow-list per privileged platform user.
* Antiforgery tokens on every unsafe MVC request (global `AutoValidateAntiforgeryToken`), Razor HTML encoding,
  parameterised queries only (EF/Dapper), security headers, HTTPS + HSTS, cookie `HttpOnly` + `Secure` + `SameSite`.
* Rate limiting on login and API endpoints (`Microsoft.AspNetCore.RateLimiting`).
* Automatic audit trail: every insert/update/delete of an `IAuditable` entity writes an `AuditLogs` row with
  actor, tenant, entity, old/new values and IP.
* Soft delete on business entities (`ISoftDelete`); organization delete is soft and requires typed confirmation.

## 6. Cross-cutting

| Concern | Approach |
|---|---|
| Logging | `ILogger` (Microsoft.Extensions.Logging); Serilog/Seq can be plugged in later. |
| Exceptions | Global exception handler page (MVC) + ProblemDetails (API). |
| Validation | DataAnnotations on view models (server) + jQuery unobtrusive validation (client). |
| Background work | `IHostedService` jobs; Hangfire can replace them when job volume grows. |
| Reporting | Dapper for heavy read/aggregate queries, EF Core for transactional writes. |
| Configuration | `appsettings.json` + environment variables; secrets never committed. |
| Statutory rules (later phases) | Data-driven rule tables with effective-from/to dates; never hard-coded thresholds. |

## 7. UI

* Bootstrap 5.3 with a small design system (`wwwroot/css/app.css`): tokens for colour, spacing, radius; light/dark
  mode via `data-bs-theme`.
* Super Admin portal: dark indigo "Control Center" theme, its own layout and navigation.
* Organization portal: light teal theme with company branding colour; navigation built from enabled features and
  user permissions.
* Shared components: KPI cards, data tables with search/filter/pagination, modal confirmations, toasts, empty states.
