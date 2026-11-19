# Codenexify HRMS — Module-wise Development Plan

Status legend: ✅ done & tested · 🟡 in progress · ⬜ not started.
A module is only ✅ when database, backend, UI, permissions, workflows and tests are all in place.

## Phase 1 — SaaS Foundation

| # | Item | Status | Notes |
|---|---|---|---|
| 1.1 | Solution architecture, Clean Architecture projects | ✅ | `docs/01-Architecture.md` |
| 1.2 | Database design & EF Core migrations | ✅ | `docs/02-Database-Design.md`, initial migration |
| 1.3 | Multi-tenant core (claim-based tenant, global filters, write guard) | ✅ | `TenantIsolationTests` |
| 1.4 | Super Admin authentication (cookie, lockout, TOTP 2FA, IP allow-list, login audit) | ✅ | |
| 1.5 | Super Admin dashboard (org/user/MRR/ARR KPIs, charts, recent activity) | ✅ | revenue from payments: `/superadmin/billing` |
| 1.6 | Organization management (create, edit, status lifecycle, soft delete, limits, filters) | ✅ | |
| 1.7 | Plan management (CRUD, pricing, trial, limits, inheritance, features) | ✅ | |
| 1.8 | Subscription management (assign, upgrade/downgrade, extend, pause, resume, cancel, renew) | ✅ | |
| 1.9 | Feature access engine (global + per-org overrides, `[RequireFeature]`) | ✅ | `FeatureAccessTests` |
| 1.10 | Platform user & permission management | ✅ | |
| 1.11 | Audit log & login history viewers | ✅ | |
| 1.12 | Self-service organization registration (trial) | ✅ | `/register` |
| 1.13 | Organization Admin portal shell, tenant login, roles & permissions, tenant users | ✅ | |
| 1.14 | Subscription lifecycle background job | ✅ | |
| 1.15 | Read-only support access with consent & audit | ✅ | see 1B.13 |

## Phase 1B — Platform commercial ops (billing & support done)
| # | Item | Status | Notes |
|---|------|--------|-------|
| 1B.1 | Platform billing settings: supplier/GSTIN, SAC, GST rate, prefixes, payment terms, reminder schedule, bank details | ✅ | `/superadmin/billing/settings` (SuperAdmin only) |
| 1B.2 | GST tax invoices: CGST+SGST (intra-state) / IGST (inter-state) by place of supply, FY-wise numbering `CNX/26-27/00001` | ✅ | printable; numbers never reused (cancel keeps the number) |
| 1B.3 | Subscription invoices (per-employee seats × agreed or plan price) and other one-off invoices | ✅ | duplicate plan+period blocked |
| 1B.4 | Offline payments (bank/UPI/cheque/cash) and Razorpay online payments (order → Checkout → server signature check) | ✅ | CSP opens Razorpay only on `/org/billing` |
| 1B.5 | Razorpay webhook `/webhooks/razorpay` (HMAC verified, idempotent with the checkout callback) | ✅ | ignores any browser session |
| 1B.6 | Paid invoice fulfils exactly once: new subscription → activate; renewal → extend one cycle | ✅ | |
| 1B.7 | Credit notes (GST split pro rata) with gateway or offline refunds; cancel only without payments | ✅ | |
| 1B.8 | Billing job (6-hourly): renewal invoices N days before end, reminders per schedule, trial-ending notices | ✅ | idempotent per day |
| 1B.9 | SMTP email via MailKit with an email log for every attempt | ✅ | |
| 1B.10 | Revenue dashboard: collected vs invoiced by month/quarter/FY, outstanding, overdue, recent payments | ✅ | `/superadmin/billing` |
| 1B.11 | Organization billing: invoices, pay online, self-service plan purchase during trial | ✅ | `/org/billing`, permission `org.billing.manage` |
| 1B.12 | Support tickets: categories, priority, status, assignment, customer ↔ support thread, staff-only internal notes, first-response time, email both ways | ✅ | `/org/support`, `/superadmin/support` |
| 1B.13 | Consent-based read-only support access: org admin grants 1–72 h (payroll opt-in), staff visit ≤ 2 h, every non-GET blocked, every page view in the org's audit trail, revocable instantly | ✅ | permissions `org.support.access`, `platform.support.access` |
| 1B.14 | Platform announcements (all orgs or one plan, schedule window, severity, dismissible except critical) | ✅ | `/superadmin/announcements` |
| 1B.15 | Maintenance mode: org portals, self-service and API return 503; Control Center and webhooks stay up | ✅ | `/superadmin/system` |
| 1B.16 | Ticket attachments, SLA targets & auto-close, SMS/WhatsApp channels, email templates | ⬜ | later |

Known limit: an **expired** organization cannot sign in, so it cannot pay online; renewal invoices are raised before the end date and the past-due grace period keeps access open for payment. The platform can still record bank transfers for expired customers, which reactivates them.

## Phase 2A — Core HR (done)

| # | Item | Status | Notes |
|---|---|---|---|
| 2.1 | Organization structure: branches (plan branch limit), departments (hierarchy, head), designations | ✅ | `StructureService`, `/org/structure` |
| 2.2 | HR settings: employee ID prefix/padding/sequence, default notice & probation | ✅ | concurrency-safe sequence |
| 2.3 | Employees: directory with filters/pagination, profile (employment, personal, education, experience) | ✅ | `EmployeeService`, `/org/employees` |
| 2.4 | Basic onboarding: auto employee ID, optional login with Employee role, plan employee limit | ✅ | |
| 2.5 | Sensitive data: PAN and bank account encrypted (Data Protection), masked display, separate permission | ✅ | never written to audit trail |
| 2.6 | Org chart with reporting-loop prevention | ✅ | `/org/employees/orgchart` |
| 2.7 | Documents: private storage, type/signature/size checks, expiry tracking, verification, storage quota | ✅ | `IFileStorage` (local disk; cloud later) |
| 2.8 | Exit: resignation (self-service) / HR-initiated, approve/reject/withdraw, configurable checklist, completion disables login | ✅ | F&F settlement → Phase 3 |
| 2.9 | Basic reports: headcount, joiners, exits, Excel export | ✅ | PDF export → with letters |
| 2.10 | Employee self-service: own profile (masked), documents, resignation | ✅ | `/me` |
| 2.11 | Upgrade seeding: new permissions granted once to existing system roles | ✅ | |
| 2.12 | Letters & templates with PDF generation (offer, appointment, confirmation, experience, relieving + custom) | ✅ | `/org/letters`; placeholders whitelisted; preview → edit → issue; numbered `CODE/HR/26-27/0001`; PDF filed in employee documents (optional share); revoke keeps record |
| 2.12a | PDF downloads for payslips (HR and employee) and GST invoices (Control Center and organization) | ✅ | PDFsharp/MigraDoc **pinned to 6.1.1 (MIT)** — 6.5+ is AGPL-3.0 |
| 2.13 | Teams, companies (legal entities) | ⬜ | multi-company is an add-on (Phase 7) |

## Phase 2B — Time & Attendance (done)

| # | Item | Status | Notes |
|---|---|---|---|
| 2.20 | Leave types with policy: quota, upfront/monthly accrual (pro-rated), carry-forward limit, encashment limit, half-day, notice, gender | ✅ | `LeaveService`, `/org/leave` |
| 2.21 | Balances: deterministic accrual, carry-forward at year start, HR adjustments & encashment (audited) | ✅ | |
| 2.22 | Applications: working-day count (weekly offs + holidays excluded), half days, overlap & balance checks, unpaid LOP | ✅ | |
| 2.23 | Approvals: reporting manager → HR fallback; HR can record leave; cancellation restores balance | ✅ | `/me/approvals`, `/org/leave` |
| 2.24 | Holiday calendar (optional holidays, branch-specific) | ✅ | |
| 2.25 | Shifts: timings, break, grace, full/half-day hours, OT threshold, weekly offs, default shift, dated assignments | ✅ | `/org/shifts` |
| 2.26 | Attendance: web clock-in/out, night-shift attribution, daily status (present/half/absent/leave/holiday/off), late & OT minutes | ✅ | `AttendanceService` |
| 2.27 | HR daily view, monthly register (Excel), regularization with lock/undo | ✅ | `/org/attendance` |
| 2.28 | Overtime claims with manager/HR approval (partial approval) | ✅ | |
| 2.29 | Gamified attendance: points, streaks, badges, leaderboard (points configurable) | ✅ | `/me/leaderboard` |
| 2.30 | In-app notifications (leave/OT requests & decisions) with bell & inbox | ✅ | email channel → Phase 1B SMTP |
| 2.31 | Hourly job: finalize attendance (absences), refresh accruals, per organization | ✅ | `TimeAttendanceJob` |
| 2.32 | Sandwich-leave rule, comp-off, leave encashment payout | ⬜ | payout with Payroll (Phase 3) |
| 2.33 | Email notifications, birthday/anniversary & document-expiry reminders | ⬜ | needs SMTP config (Phase 1B) |

Known limits: the monthly register and leaderboard recalculate on demand (fine for SMB tenants; a nightly
materialization should replace this for organizations with thousands of employees). GPS/selfie/geo-fenced punches
are Strength features (Phase 5).

## Phase 3 — Payroll (core done)

| # | Item | Status | Notes |
|---|---|---|---|
| 3.1 | Platform statutory rules: PF/EPS/EDLI/admin, ESI, gratuity (effective-dated), PT slabs & LWF per state, income-tax slabs/rebate/surcharge/cess per FY & regime | ✅ | Control Center → Statutory rules; seeded values are **unverified** until checked |
| 3.2 | Salary components & structures (fixed, % of gross, % of basic, balancing) | ✅ | |
| 3.3 | Employee salaries with dated revisions, live breakup & CTC | ✅ | revisions can't touch finalized months |
| 3.4 | Payroll run: attendance LOP (absent/half-day), unpaid leave, mid-month joiners/leavers, LOP override, hold | ✅ | `PayrollEngine` (pure, unit tested) |
| 3.5 | Statutory deductions: PF (ceiling option), ESI (eligibility + round-up), PT (state, gender, special month), LWF | ✅ | |
| 3.6 | TDS: annual projection with YTD, old/new regime, HRA exemption, 80C (+PF)/80D/80CCD(1B)/24b, 87A rebate + marginal relief, surcharge, cess | ✅ | surcharge marginal relief not applied |
| 3.7 | Approved overtime pay, leave-encashment payout (marked paid at finalization) | ✅ | |
| 3.8 | Loans & salary advances: request/approve, instalment recovery, manual repayment | ✅ | |
| 3.9 | Finalize (locks, recovers loans, publishes payslips, notifications), mark paid | ✅ | |
| 3.10 | Payslips (printable / save as PDF), salary register, bank transfer, PF & ESI statements, accounting journal (Excel) | ✅ | server-side PDF library not added |
| 3.11 | Tax declarations with regime comparison; HR approve/reopen | ✅ | proofs are uploaded as employee documents |
| 3.12 | Gratuity liability; full & final settlement (encashment, gratuity, loan recovery, manual items) | ✅ | final month salary via regular payroll |
| 3.13 | One-time items (bonus, incentive, reimbursement, recovery): paid with a month's payroll, settled at finalization, carried forward while a salary is on hold; never PF/ESI wages | ✅ | Payroll → One-time items |
| 3.14 | Arrears for back-dated revisions: difference vs what was actually paid per finalized month (prorated from the effective date), as arrears or recovery in the next payroll | ✅ | PF/ESI on arrears not auto-added (noted on the item) |
| 3.15 | Expense claims: categories with limits/receipt rule, receipts (PDF/PNG/JPG, signature-checked), manager → finance approval, paid via payroll (non-taxable) or directly | ✅ | `/me/expenses`, `/org/expenses`, permission `org.expenses.manage` |
| 3.16 | EPFO ECR 2.0 text file (refuses if a PF member has no UAN) | ✅ | run → Reports |
| 3.17 | Form 16 Part B PDF (HR and employee self-service; provisional until FY end) and Form 24Q deductee annexure (Excel) | ✅ | Part A comes from TRACES; 24Q needs sensitive-data permission |
| 3.18 | Perks & benefits: plans (health/life/accident insurance, allowances, meal, transport, wellness, education) with provider, policy, cover, renewal date and policy PDF; company cost, employee contribution (+ per dependent) and allowance per month; automatic or opt-in enrolment with an optional window; eligibility by employment type, service days, branch, department, probation; hourly sync enrols newly eligible staff and ends leavers; HR exceptions; family members (minimal: name, relation, birth date); payroll pays allowances (taxable flag, never PF/ESI wages) and deducts contributions for each month a membership is active; insurer roster export | ✅ | `/org/benefits`, `/me/benefits`, permission `org.benefits.manage`, feature `BENEFITS` |
| 3.19 | Statutory bonus (Payment of Bonus Act), FVU/RPU file generation, insurance claims tracking | ⬜ | later |

## Phase 4 — Employee Self-Service & Experience (core done)
| # | Item | Status | Notes |
|---|------|--------|-------|
| 4.1 | Social wall: posts, likes, comments; author or moderator removes (soft delete) | ✅ | `/me/wall`, feature `SOCIAL_WALL` |
| 4.2 | Praise: up to 10 current colleagues, fixed badge list, never yourself; recipients notified | ✅ | |
| 4.3 | Polls: 2–6 options, one anonymous vote, results after voting or when closed | ✅ | |
| 4.4 | HR announcements on the wall: pin for N days, notify everyone, comments optional | ✅ | permission `org.wall.moderate` (Org Admin, HR Admin) |
| 4.5 | Profile change requests: employee proposes personal-detail corrections, HR compares current vs requested and approves/rejects; no self-approval | ✅ | `/me/profile`, `/org/employees/requests`; bank/PAN/job data stay HR-only |
| 4.6 | People directory (work details only) and celebrations (birthdays without year, work anniversaries; employee can hide birthday) | ✅ | `/me/people`, feature `ESS` / `NOTIFICATIONS_BASIC` |
| 4.7 | Email copies of in-app notifications (1-minute dispatcher; plan, user opt-out and 2-day freshness respected) | ✅ | set `Platform:PublicUrl` for links in emails |
| 4.8 | Mobile API (bearer): leave balances/requests/apply/cancel, attendance today/punch, payslips, notifications, wall, directory, celebrations | ✅ | `/api/v1/me/*`, `/api/v1/wall`, `/api/v1/directory`; enums as names |
| 4.9 | Wall images/attachments, @mentions, department-targeted announcements, push notifications | ⬜ | later |

## Phase 5 — Strength (done)
| # | Item | Status | Notes |
|---|------|--------|-------|
| 5.1 | Asset register: tag (unique), category, serial, cost, warranty, condition; assign → employee confirms receipt → take back (damaged → under repair), lost/retired; full history | ✅ | `/org/assets`, `/me/assets`, permission `org.assets.manage`, feature `ASSETS` |
| 5.2 | GPS attendance: Off / Record / Enforce per organization; location captured only at the moment of a punch (web and mobile API) | ✅ | `/org/attendance/locations`, feature `GPS_ATTENDANCE`; browser permission policy allows geolocation only on `/me` and the locations page |
| 5.3 | Geo-fence: nearest active office within radius (+ GPS accuracy allowance capped at 100 m), fixes worse than 500 m refused, (0,0) treated as missing; per-employee exemption; HR review of located punches | ✅ | feature `GEO_FENCING`; `GeoFence` is pure and unit tested |
| 5.4 | Multiple office locations (otherwise one active location) | ✅ | feature `MULTI_LOCATION` |
| 5.5 | Pulse / engagement / exit surveys: rating, eNPS, multiple choice, free text; draft → publish (notifies audience) → auto-close; department targeting; exit surveys go to employees with a pending/approved exit | ✅ | `/org/surveys`, `/me/surveys`, features `PULSE_SURVEYS`, `EXIT_SURVEY` |
| 5.6 | Survey anonymity: participation (who) and response (what) stored apart with no link; responses carry the date only, no author, no audit rows, no per-response counter; results hidden below the minimum (≥ 3 for anonymous) | ✅ | marker `IAnonymousRecord` in the domain |
| 5.7 | Anonymous feedback: sender gets a one-time private code (only its SHA-256 hash stored, sent in POST bodies only), HR replies, sender follows up with the code, HR closes | ✅ | `/me/feedback`, `/org/feedback`, feature `ANONYMOUS_FEEDBACK` |
| 5.8 | Employee timeline: joining, confirmation, exit, leave, letters, documents, assets, praise, profile changes; salary revisions and loans only for users who can see payroll | ✅ | Employees → Timeline, feature `EMPLOYEE_TIMELINE` |
| 5.9 | People analytics: headcount, 12-month joiners/leavers and attrition, 30-day attendance rate, tenure, department/gender/tenure/age mix | ✅ | `/org/analytics`, permission `org.analytics.view` |
| 5.10 | Custom roles hardened: nobody can grant (via a role or a role assignment) permissions they don't hold; only Organization Admins hand out or change Organization Admins; duplicate a role; plan-gated permissions marked | ✅ | `TenantAdminService` |
| 5.11 | Advanced onboarding: templates of HR / manager / buddy / joiner tasks due relative to joining; per-joiner plans copied from a template; self-service task lists; completion notifies HR | ✅ | `/org/onboarding`, `/me/onboarding`, permission `org.onboarding.manage`; every org gets an editable “Standard onboarding” template |
| 5.12 | Travel desk: request → reporting manager approves (desk approves when there's no active manager) → desk books with details and ticket (PDF/PNG/JPG, signature-checked) → advance recorded → closed after return with a prompt to claim expenses | ✅ | `/me/travel`, `/org/travel`, permission `org.travel.manage` |
| 5.13 | Selfie clock-in: optional camera photo with each self-service punch (web + mobile API), HR review by day, automatic deletion after 7–365 days | ✅ | `/org/attendance/selfies`; no face recognition; camera allowed only on `/me` |
| 5.14 | In-app e-signature of issued letters: emailed 6-digit code (10 min, 5 tries), typed full name, consent; signed PDF with an acceptance certificate (time, IP, SHA-256 of the text) added to the employee's documents; decline with reason | ✅ | `/org/letters/signatures`, `/me/sign`; not an Aadhaar eSign — see known limits |
| 5.15 | Advanced notifications: reminder rules (probation ending, document expiring, work anniversary, birthday, overdue onboarding task) to employee / manager / HR, hourly, once per subject | ✅ | `/org/reminders`, permission `org.notifications.manage`; email copies via the existing dispatcher |
| 5.16 | Advanced reports: department / branch / employment-type filters on HR reports and exports; weekly or monthly summary emails with a sign-in link (no attachments) | ✅ | `/org/reports/schedules` |
| 5.17 | Single sign-on with Google and Microsoft (OpenID Connect, code + PKCE); organization lists its email domains; only existing users; Microsoft issuer pinned to the token's tenant | ✅ | Control Center → Billing settings for app registrations; `/org/security` per organization |
| 5.18 | Mobile OTP sign-in via MSG91 (DLT template): 6-digit code, 5 minutes, single use, 3 sends per 15 min, wrong codes count toward lockout, responses never reveal whether a number exists | ✅ | `/login/methods`, `/login/otp` |
| 5.21 | Legal pages: Privacy Policy, Terms of Service, Refund & Cancellation Policy, Contact Us on the public site (footer and sign-in pages); text edited in the Control Center in a safe mini-format with placeholders filled from settings (company, address, GSTIN, grievance officer, jurisdiction); starter drafts marked "not reviewed"; versions; self-registration requires ticking acceptance, and the date, versions and IP are stored on the organization | ✅ | `/privacy`, `/terms`, `/refund-policy`, `/contact`, `/superadmin/legal`; drafts are not legal advice |
| 5.20 | Forgot password for company users and Control Center staff: emailed one-time link (32 random bytes, only SHA-256 stored), 30 minutes, 3 per hour, a new link replaces the old one; same reply whether or not the account exists; reset ends other sessions, clears lockout, keeps 2FA, emails a "password changed" notice, logs to login history; allow-listed staff only from allowed networks | ✅ | `/forgot-password`, `/superadmin/account/forgot-password`; needs SMTP + `Platform:PublicUrl` |
| 5.19 | Control Center “Reset all data”: Super Admin only, off unless `Platform:AllowDataReset` is true, typed phrase + password (+ authenticator code), verified SQL Server backup first, one-transaction wipe of every non-platform table, uploads moved aside, audit entry | ✅ | `/superadmin/system/reset` |

Known limits: web-server request logs still record which signed-in user called `/me/feedback` and when; keep
request logging at the default level (no per-request user logging) if anonymity must also hold against server
operators. Survey eligibility is evaluated per open survey on page load (fine for SMB tenants).
In-app e-signature is an electronic acceptance record, not an Aadhaar eSign or digital signature certificate under
the IT Act; use a licensed eSign provider where law requires one. SSO, SMS and e-sign emails need the platform's
app registrations / MSG91 account / SMTP to be configured; until then the options stay hidden or explain why.

## Phase 6 — Growth (in progress)
| # | Item | Status | Notes |
|---|------|--------|-------|
| 6.1 | Goals & OKRs: company, department and individual goals; alignment to company/department goals (no loops); key results (number, %, ₹, milestone; growth or reduction targets) with automatic progress; check-ins with status (on track / at risk / off track / completed) and history; employees set their own, managers set them for anyone in their reporting line, HR sets company and department goals; individual goals visible only to the owner, their managers and HR | ✅ | `/me/goals`, permission `org.goals.manage`, feature `OKR_GOALS` |
| 6.2 | Performance reviews: cycles with period, due dates, eligibility (joined by, department), 1–5 scale with editable labels, starter form (editable before launch: rating/written, employee/manager/both, optional); launch assigns the reporting manager (HR when none); self-review (draft, submit) → manager review (after self-review or its due date) with overall rating; employee sees the manager part only after HR shares results; HR calibrates (reason required), shares, closes; late reviews shared on submit; employee acknowledges with a comment; goals of the period shown in the form; rating distribution; Excel export | ✅ | `/org/reviews`, `/me/reviews`, permission `org.reviews.manage`, feature `PERFORMANCE_REVIEWS` |
| 6.3 | Continuous feedback: appreciation or suggestion to any colleague, named, with the writer choosing who reads it (only the person / person and managers / managers only); requests for feedback about yourself or someone in your team (up to 10 people, optional reply-by), answered or declined once; managers see their team's; feedback from the review period appears beside the review (person sees theirs, reviewers and HR see the managers' view) | ✅ | `/me/feedback-hub`, feature `CONTINUOUS_FEEDBACK` |
| 6.4 | One-on-ones between a manager and anyone in their reporting line (either side books); date, time, length, repeat weekly / fortnightly / monthly; shared agenda; action items with owner and due date that carry over until done; shared notes plus private notes for each side; move, cancel, mark held (schedules the next one in a series) | ✅ | `/me/one-on-ones`, feature `ONE_ON_ONE` |
| 6.5 | Core values: HR defines up to 12 values (name, meaning, icon, order); public shout-outs tie a colleague's action to a value (person and their manager notified; 20 a day per sender; author or HR can remove); values wall with counts and most-recognised list; one click adds a rating question per value to a draft review cycle; reviews show how often the person was recognised for each value | ✅ | `/me/values`, `/org/values`, permission `org.values.manage`, feature `CORE_VALUES` |
| 6.6 | Skills: HR skill catalog by category and required levels per job title; employees rate themselves 1–5 (Beginner–Expert); managers in the reporting line (and HR) add an assessment with a note, which counts over the self-rating; gaps against the job title; find people by skill and minimum level; skills matrix to Excel | ✅ | `/me/skills`, `/org/skills`, permission `org.skills.manage`, feature `SKILLS` |
| 6.7 | Promotions: managers propose for anyone in their reporting line (HR for anyone) a new job title and/or annual gross with an effective date, justification and optional linked review; one open case per employee; HR approves (or rejects with a reason); on the effective date the title changes and a salary revision is added with the current structure (back-dated dates pay arrears); failures are shown for retry; proposer and employee notified | ✅ | `/me/promotions`, `/org/promotions`, permission `org.promotions.manage`, feature `PROMOTIONS` |
| 6.8 | Performance improvement plans: managers (or HR) draft a 14–180-day plan with factual concerns, support offered and measurable objectives; HR starts it (only then does the employee see it); the employee acknowledges with comments; manager/HR check-ins and objective status; HR extends (twice at most) and records the outcome (passed / not passed / cancelled) | ✅ | `/me/pips`, `/org/pips`, permission `org.pip.manage`, feature `PIP` |
| 6.9 | Compensation plans (salary-revision cycles): budget as % of eligible pay, raise guidelines per rating (HR sets the ranges; none are built in), ratings taken from a review cycle; launch creates a line per employee with a salary, planned by their reporting manager (HR when none); budget vs planned per manager; raises outside the guideline need a note; HR closes planning, adjusts, approves (everyone needs a decision; 0% allowed); revisions applied on the effective date with the current structure (arrears if back-dated), failures shown with retry; employee notified; Excel export | ✅ | `/org/compensation`, `/me/compensation`, permission `org.compensation.manage`, feature `COMPENSATION_PLANS` |
| 6.10 | Calibration board: a review cycle's submitted ratings side by side by rating, filterable by department and reviewer, against an optional target distribution (must total 100%; columns more than 5 points off are flagged); move someone with a reason (recorded as the review's HR adjustment, original rating shown); read-only once results are shared | ✅ | `/org/reviews/{id}/calibration`, permission `org.reviews.manage`, feature `CALIBRATION` |
| 6.11 | Workflow automation: rules triggered by an employee being added, N days before joining / probation end / last day, or a promotion taking effect; optional department, branch and employment-type filters; up to 10 actions each (notify the employee, manager, HR team or a named user; create a task with a due date; start onboarding from a template without buddy tasks; enrol in a benefit plan) with `{Employee}` `{Date}` `{Department}` `{Manager}` placeholders; runs hourly and on demand, once per employee per event, with a run log; "My tasks" page for assigned tasks | ✅ | `/org/workflows`, `/me/tasks`, permission `org.workflows.manage`, feature `WORKFLOW_AUTOMATION` |
| 6.12 | Custom report builder: employees, leave, attendance (per month) or payroll (finalized runs, needs payroll access) as the source; choose columns from a fixed list, filter by department, branch, employment type, current/former staff and dates; group with row counts and summed numbers; sort; totals; up to 5,000 rows; export to Excel; save (max 50 per user) and share read-only with other report builders | ✅ | `/org/report-builder`, permission `org.reports.build`, feature `CUSTOM_REPORT_BUILDER` |
| 6.13 | Headcount planning: plans for a period (draft → approved → closed, reopenable); positions per department with openings, new role or replacement, target start, budget per hire, priority and status; link people who joined in the period to a position (same department, one position per plan); per-department view of headcount today, joining, known exits, openings, filled, still to hire, projected headcount and budget, plus committed cost from hires' actual salaries for payroll users; unlinked joiners listed; Excel export | ✅ | `/org/headcount`, permission `org.headcount.manage`, feature `HEADCOUNT_PLANNING` |
| 6.14 | Pre-boarding: HR sets a welcome message, first-day details, contact and a checklist (document uploads, read-and-accept policies); emails a new hire a secure personal link (only a SHA-256 hash stored; works until a set number of days after joining; new link replaces the old and keeps answers; HR can cancel or reopen). Without signing in, the new hire sees their team, manager and first-day info, fills personal details (no Aadhaar), PAN and bank account (stored encrypted, shown back masked), uploads documents (type and content checked) and accepts policies, then sends it to HR, who is notified. Signed-in sessions are kept out of the link page; it is rate-limited, never cached or indexed | ✅ | `/org/preboarding`, `/welcome?token=…`, permission `org.preboarding.manage`, feature `PREBOARDING` |

Fixed while building 6.2: buttons that carry a value (Save draft / Submit, Approve / Reject, Preview, Close conversation) lost it because
the double-submit guard disabled them first, so tax declarations stayed drafts and travel/expense approvals arrived as rejections; and a
confirmation set on a button was ignored. Registering a new company while signed in to another one no longer crashes (it asks to sign out).

## Phase 7 — Premium · Phase 8 — Commercial launch
As listed in the master specification; each starts with its tables, feature codes (already seeded in `Features`)
and permission codes.

## Working rules
1. Each module is behind a feature code and permission codes.
2. Every tenant table derives from `TenantEntity`.
3. Every service that creates employees/branches checks plan limits.
4. Each module ships with: migration, services, controllers/API, views, tests, docs update here.
