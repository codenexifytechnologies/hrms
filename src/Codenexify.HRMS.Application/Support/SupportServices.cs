using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Codenexify.HRMS.Application.Support;

// ====================================================================== tickets

public sealed class TicketInput
{
    [Required, StringLength(200, MinimumLength = 5)] public string Subject { get; set; } = string.Empty;
    public TicketCategory Category { get; set; } = TicketCategory.General;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    [Required, StringLength(4000, MinimumLength = 10), Display(Name = "Describe the problem")] public string Body { get; set; } = string.Empty;
}

public sealed class TicketQuery
{
    public TicketStatus? Status { get; set; }
    public bool OpenOnly { get; set; }
    public bool MineOnly { get; set; }
    public int? OrganizationId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record TicketRow(int Id, int OrganizationId, string OrganizationName, string Subject, TicketCategory Category, TicketPriority Priority,
    TicketStatus Status, string RaisedByName, string? AssignedToName, DateTime CreatedAtUtc, DateTime LastActivityAtUtc, bool AwaitingSupport);

public sealed record TicketView(SupportTicket Ticket, string OrganizationName, IReadOnlyList<TicketMessage> Messages);

public sealed record TicketCounts(int Open, int InProgress, int WaitingOnCustomer, int Unassigned, int Urgent);

public interface ISupportTicketService
{
    Task<PagedResult<TicketRow>> ListAsync(TicketQuery query, CancellationToken ct = default);
    Task<TicketView?> GetAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(TicketInput input, CancellationToken ct = default);
    /// <param name="internalNote">Platform only: a note the organization never sees.</param>
    Task<Result> ReplyAsync(int id, string body, bool internalNote = false, TicketStatus? newStatus = null, CancellationToken ct = default);
    Task<Result> CloseAsync(int id, CancellationToken ct = default);
    Task<Result> ReopenAsync(int id, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, TicketStatus status, TicketPriority priority, int? assigneeId, CancellationToken ct = default);
    Task<TicketCounts> CountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CoreHr.LookupItem>> AssigneesAsync(CancellationToken ct = default);
}

public sealed class SupportTicketService(IAppDbContext db, ICurrentContext context, IClock clock, IEmailSender email, IAuditWriter audit) : ISupportTicketService
{
    private const int PageSize = 25;

    private void EnsurePlatform()
    {
        if (!context.IsPlatform) throw new TenantIsolationException("This support operation requires a platform principal.");
    }

    private int TenantOrg => context.OrganizationId ?? throw new TenantIsolationException("No organization.");

    public async Task<PagedResult<TicketRow>> ListAsync(TicketQuery q, CancellationToken ct = default)
    {
        // Tenants are confined by the global query filter; platform staff may filter by organization.
        var query = db.SupportTickets.AsNoTracking();
        if (context.IsPlatform && q.OrganizationId is { } org) query = query.Where(t => t.OrganizationId == org);
        if (!context.IsPlatform && q.MineOnly) query = query.Where(t => t.RaisedByUserId == context.UserId);
        if (context.IsPlatform && q.MineOnly) query = query.Where(t => t.AssignedToPlatformUserId == context.UserId);
        if (q.Status is { } s) query = query.Where(t => t.Status == s);
        if (q.OpenOnly) query = query.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = int.TryParse(term.TrimStart('#'), out var id) ? query.Where(t => t.Id == id) : query.Where(t => t.Subject.Contains(term));
        }
        var page = Math.Max(1, q.Page);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(t => t.Priority == TicketPriority.Urgent && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .ThenByDescending(t => t.LastActivityAtUtc)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(t => new
            {
                t.Id, t.OrganizationId, Org = db.Organizations.Where(o => o.Id == t.OrganizationId).Select(o => o.Name).FirstOrDefault(),
                t.Subject, t.Category, t.Priority, t.Status, t.RaisedByName, t.AssignedToName, t.CreatedAtUtc, t.LastActivityAtUtc,
                LastAuthor = t.Messages.Where(m => !m.IsInternal).OrderByDescending(m => m.Id).Select(m => (TicketAuthor?)m.Author).FirstOrDefault()
            }).ToListAsync(ct);
        return new PagedResult<TicketRow>(rows.Select(r => new TicketRow(r.Id, r.OrganizationId, r.Org ?? "", r.Subject, r.Category, r.Priority, r.Status,
            r.RaisedByName, r.AssignedToName, r.CreatedAtUtc, r.LastActivityAtUtc,
            r.LastAuthor == TicketAuthor.Customer && r.Status is not (TicketStatus.Resolved or TicketStatus.Closed))).ToList(), total, page, PageSize);
    }

    public async Task<TicketView?> GetAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SupportTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        // Internal notes are filtered on the server, never just hidden in the view.
        var messages = await db.TicketMessages.AsNoTracking().Where(m => m.TicketId == id && (context.IsPlatform || !m.IsInternal))
            .OrderBy(m => m.Id).ToListAsync(ct);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == t.OrganizationId).Select(o => o.Name).FirstAsync(ct);
        return new TicketView(t, org, messages);
    }

    public async Task<Result<int>> CreateAsync(TicketInput input, CancellationToken ct = default)
    {
        var orgId = TenantOrg;
        var user = await db.Users.AsNoTracking().Where(u => u.Id == context.UserId).Select(u => new { u.FullName, u.Email }).FirstOrDefaultAsync(ct);
        if (user is null) return Result<int>.Fail("Only an organization user can raise a ticket.");
        var now = clock.UtcNow;
        var ticket = new SupportTicket
        {
            OrganizationId = orgId, Subject = input.Subject.Trim(), Category = input.Category,
            Priority = input.Priority, RaisedByUserId = context.UserId, RaisedByName = user.FullName, RaisedByEmail = user.Email, LastActivityAtUtc = now,
            Messages = [new TicketMessage { OrganizationId = orgId, Author = TicketAuthor.Customer, AuthorUserId = context.UserId, AuthorName = user.FullName, Body = input.Body.Trim() }]
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        await NotifySupportAsync(ticket, $"New ticket #{ticket.Id}: {ticket.Subject}", input.Body, ct);
        return Result<int>.Ok(ticket.Id);
    }

    public async Task<Result> ReplyAsync(int id, string body, bool internalNote = false, TicketStatus? newStatus = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) return Result.Fail("Write a message.");
        if (body.Length > 4000) return Result.Fail("The message is too long (4000 characters at most).");
        if (internalNote && !context.IsPlatform) return Result.Fail("Only support staff can add internal notes.");
        var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Ticket not found.");
        if (t.Status == TicketStatus.Closed && !context.IsPlatform) return Result.Fail("This ticket is closed. Reopen it to reply.");
        var now = clock.UtcNow;
        string author;
        if (context.IsPlatform)
        {
            author = await db.PlatformUsers.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Codenexify Support";
            if (!internalNote)
            {
                t.FirstResponseAtUtc ??= now;
                t.Status = newStatus ?? (t.Status is TicketStatus.Open ? TicketStatus.WaitingOnCustomer : t.Status);
                if (t.Status == TicketStatus.Resolved) t.ResolvedAtUtc = now;
            }
            else if (newStatus is { } s) t.Status = s;
        }
        else
        {
            author = await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Customer";
            // A customer reply puts the ticket back in the support queue.
            if (t.Status is TicketStatus.WaitingOnCustomer or TicketStatus.Resolved) { t.Status = TicketStatus.Open; t.ResolvedAtUtc = null; }
        }
        db.TicketMessages.Add(new TicketMessage
        {
            OrganizationId = t.OrganizationId, TicketId = t.Id, Author = context.IsPlatform ? TicketAuthor.Support : TicketAuthor.Customer,
            AuthorUserId = context.UserId, AuthorName = author, Body = body.Trim(), IsInternal = internalNote
        });
        t.LastActivityAtUtc = now;
        await db.SaveChangesAsync(ct);

        if (!internalNote)
        {
            if (context.IsPlatform) await NotifyCustomerAsync(t, $"Re: [#{t.Id}] {t.Subject}", body, ct);
            else await NotifySupportAsync(t, $"Customer reply on #{t.Id}: {t.Subject}", body, ct);
        }
        return Result.Ok();
    }

    public async Task<Result> CloseAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Ticket not found.");
        if (t.Status == TicketStatus.Closed) return Result.Fail("The ticket is already closed.");
        t.Status = TicketStatus.Closed;
        t.ResolvedAtUtc ??= clock.UtcNow;
        t.LastActivityAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReopenAsync(int id, CancellationToken ct = default)
    {
        var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Ticket not found.");
        if (t.Status is not (TicketStatus.Closed or TicketStatus.Resolved)) return Result.Fail("The ticket is still open.");
        t.Status = TicketStatus.Open;
        t.ResolvedAtUtc = null;
        t.LastActivityAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(int id, TicketStatus status, TicketPriority priority, int? assigneeId, CancellationToken ct = default)
    {
        EnsurePlatform();
        var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Ticket not found.");
        string? assigneeName = null;
        if (assigneeId is int a)
        {
            assigneeName = await db.PlatformUsers.Where(u => u.Id == a && u.IsActive).Select(u => u.FullName).FirstOrDefaultAsync(ct);
            if (assigneeName is null) return Result.Fail("Choose an active staff member.");
        }
        t.Status = status;
        t.Priority = priority;
        t.AssignedToPlatformUserId = assigneeId;
        t.AssignedToName = assigneeName;
        if (status is TicketStatus.Resolved or TicketStatus.Closed) t.ResolvedAtUtc ??= clock.UtcNow; else t.ResolvedAtUtc = null;
        t.LastActivityAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<TicketCounts> CountsAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        var open = db.SupportTickets.AsNoTracking().Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        return new TicketCounts(
            await open.CountAsync(t => t.Status == TicketStatus.Open, ct),
            await open.CountAsync(t => t.Status == TicketStatus.InProgress, ct),
            await open.CountAsync(t => t.Status == TicketStatus.WaitingOnCustomer, ct),
            await open.CountAsync(t => t.AssignedToPlatformUserId == null, ct),
            await open.CountAsync(t => t.Priority == TicketPriority.Urgent, ct));
    }

    public async Task<IReadOnlyList<CoreHr.LookupItem>> AssigneesAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        return await db.PlatformUsers.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName).Select(u => new CoreHr.LookupItem(u.Id, u.FullName)).ToListAsync(ct);
    }

    private async Task NotifySupportAsync(SupportTicket t, string subject, string body, CancellationToken ct)
    {
        var inbox = await db.PlatformSettings.AsNoTracking().Select(s => s.SupportEmail).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(inbox)) return;
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == t.OrganizationId).Select(o => o.Name).FirstAsync(ct);
        await email.SendAsync(new EmailMessage(inbox, subject, SupportEmails.Ticket($"{org} · {t.Category} · {t.Priority}", body, "Open it in the Control Center under Support."), "support", t.OrganizationId), ct);
    }

    private async Task NotifyCustomerAsync(SupportTicket t, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(t.RaisedByEmail)) return;
        await email.SendAsync(new EmailMessage(t.RaisedByEmail, subject, SupportEmails.Ticket($"Ticket #{t.Id} · {t.Status}", body, "Reply from Administration → Support in your HRMS."), "support", t.OrganizationId), ct);
    }
}

public static class SupportEmails
{
    private static string E(string s) => System.Net.WebUtility.HtmlEncode(s);

    public static string Ticket(string heading, string body, string footer) =>
        $"<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;color:#1e293b\"><p style=\"color:#64748b\">{E(heading)}</p>" +
        $"<div style=\"white-space:pre-wrap;border-left:3px solid #4f46e5;padding-left:12px\">{E(body)}</div>" +
        $"<p style=\"color:#64748b;font-size:12px;margin-top:24px\">{E(footer)}</p></div>";
}

// ====================================================================== read-only support access

public sealed class GrantInput
{
    [Range(1, 72), Display(Name = "Allow access for (hours)")] public int Hours { get; set; } = 24;
    [Required, StringLength(300, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
    [Display(Name = "Related ticket")] public int? TicketId { get; set; }
    [Display(Name = "Include payroll (salaries, payslips, payroll runs)")] public bool IncludePayroll { get; set; }
}

public sealed record GrantRow(int Id, int OrganizationId, string OrganizationName, string GrantedByName, string Reason, int? TicketId, bool IncludePayroll,
    DateTime GrantedAtUtc, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc, bool Active, IReadOnlyList<SessionRow> Sessions);

public sealed record SessionRow(int Id, string StaffName, DateTime StartedAtUtc, DateTime ExpiresAtUtc, DateTime? EndedAtUtc, int PageViews);

/// <summary>Everything the web layer needs to sign the staff member into the organization portal read-only.</summary>
public sealed record SupportSessionTicket(int SessionId, int OrganizationId, string OrganizationCode, string OrganizationName,
    string StaffName, string StaffEmail, DateTime ExpiresAtUtc, IReadOnlyList<string> Permissions);

public interface ISupportAccessService
{
    // organization
    Task<Result> GrantAsync(GrantInput input, CancellationToken ct = default);
    Task<Result> RevokeAsync(int grantId, CancellationToken ct = default);
    /// <summary>Tenant: own grants. Platform: currently active grants of all organizations.</summary>
    Task<IReadOnlyList<GrantRow>> GrantsAsync(CancellationToken ct = default);

    // platform
    Task<Result<SupportSessionTicket>> StartSessionAsync(int grantId, CancellationToken ct = default);

    // the running session (tenant scope of the visited organization)
    Task<bool> IsSessionActiveAsync(int sessionId, CancellationToken ct = default);
    Task RecordViewAsync(int sessionId, string path, CancellationToken ct = default);
    Task EndSessionAsync(int sessionId, CancellationToken ct = default);
}

public sealed class SupportAccessService(IAppDbContext db, ICurrentContext context, IClock clock, IAuditWriter audit) : ISupportAccessService
{
    /// <summary>A single visit never outlasts this, even when consent runs longer.</summary>
    public static readonly TimeSpan MaxSession = TimeSpan.FromHours(2);

    public async Task<Result> GrantAsync(GrantInput input, CancellationToken ct = default)
    {
        var orgId = context.OrganizationId ?? throw new TenantIsolationException("No organization.");
        if (context.IsPlatform) throw new TenantIsolationException("Only the organization can grant access.");
        var user = await db.Users.AsNoTracking().Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        if (user is null) return Result.Fail("Only an organization user can grant access.");
        if (input.TicketId is int tid && !await db.SupportTickets.AnyAsync(t => t.Id == tid, ct)) return Result.Fail("Ticket not found.");
        var now = clock.UtcNow;
        db.SupportAccessGrants.Add(new SupportAccessGrant
        {
            OrganizationId = orgId, GrantedByUserId = context.UserId!.Value, GrantedByName = user, Reason = input.Reason.Trim(), TicketId = input.TicketId,
            IncludePayroll = input.IncludePayroll, GrantedAtUtc = now, ExpiresAtUtc = now.AddHours(input.Hours)
        });
        audit.Add("SupportAccess.Grant", nameof(SupportAccessGrant), null, $"{input.Hours}h{(input.IncludePayroll ? " incl. payroll" : "")}: {input.Reason}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RevokeAsync(int grantId, CancellationToken ct = default)
    {
        if (context.IsPlatform) throw new TenantIsolationException("Only the organization can revoke access.");
        var g = await db.SupportAccessGrants.FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (g is null) return Result.Fail("Access grant not found.");
        var now = clock.UtcNow;
        if (!g.IsActive(now)) return Result.Fail("This access has already ended.");
        g.RevokedAtUtc = now;
        g.RevokedBy = await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Organization";
        foreach (var s in await db.SupportSessions.Where(s => s.GrantId == grantId && s.EndedAtUtc == null).ToListAsync(ct)) s.EndedAtUtc = now;
        audit.Add("SupportAccess.Revoke", nameof(SupportAccessGrant), grantId.ToString(), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<GrantRow>> GrantsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var q = db.SupportAccessGrants.AsNoTracking();
        if (context.IsPlatform) q = q.Where(g => g.RevokedAtUtc == null && g.ExpiresAtUtc > now);
        else q = q.Where(g => g.GrantedAtUtc > now.AddDays(-90));
        var grants = await q.OrderByDescending(g => g.Id).Take(100)
            .Select(g => new { g, Org = db.Organizations.Where(o => o.Id == g.OrganizationId).Select(o => o.Name).FirstOrDefault() }).ToListAsync(ct);
        var ids = grants.Select(x => x.g.Id).ToList();
        var sessions = await db.SupportSessions.AsNoTracking().Where(s => ids.Contains(s.GrantId)).OrderByDescending(s => s.Id).ToListAsync(ct);
        return grants.Select(x => new GrantRow(x.g.Id, x.g.OrganizationId, x.Org ?? "", x.g.GrantedByName, x.g.Reason, x.g.TicketId, x.g.IncludePayroll,
            x.g.GrantedAtUtc, x.g.ExpiresAtUtc, x.g.RevokedAtUtc, x.g.IsActive(now),
            sessions.Where(s => s.GrantId == x.g.Id).Select(s => new SessionRow(s.Id, s.PlatformUserName, s.StartedAtUtc, s.ExpiresAtUtc, s.EndedAtUtc, s.PageViews)).ToList())).ToList();
    }

    public async Task<Result<SupportSessionTicket>> StartSessionAsync(int grantId, CancellationToken ct = default)
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Support sessions are started by platform staff.");
        var now = clock.UtcNow;
        var g = await db.SupportAccessGrants.FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (g is null || !g.IsActive(now)) return Result<SupportSessionTicket>.Fail("This organization has not granted access, or the access has ended.");
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == g.OrganizationId, ct);
        if (org is null || !org.IsOperational) return Result<SupportSessionTicket>.Fail("The organization is not active.");
        var staff = await db.PlatformUsers.AsNoTracking().Where(u => u.Id == context.UserId && u.IsActive).Select(u => new { u.FullName, u.Email }).FirstOrDefaultAsync(ct);
        if (staff is null) return Result<SupportSessionTicket>.Fail("Your staff account is not active.");
        var expires = now + MaxSession < g.ExpiresAtUtc ? now + MaxSession : g.ExpiresAtUtc;
        var session = new SupportSession
        {
            OrganizationId = g.OrganizationId, GrantId = g.Id, PlatformUserId = context.UserId!.Value, PlatformUserName = staff.FullName,
            StartedAtUtc = now, ExpiresAtUtc = expires
        };
        db.SupportSessions.Add(session);
        audit.Add("SupportSession.Start", nameof(SupportSession), null, $"{staff.FullName} (read-only, grant {g.Id})", g.OrganizationId);
        await db.SaveChangesAsync(ct);
        return Result<SupportSessionTicket>.Ok(new SupportSessionTicket(session.Id, org.Id, org.Code, org.Name, staff.FullName, staff.Email, expires,
            TenantPermissions.SupportSessionPermissions(g.IncludePayroll)));
    }

    public async Task<bool> IsSessionActiveAsync(int sessionId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return await db.SupportSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.EndedAtUtc == null && s.ExpiresAtUtc > now
            && s.Grant.RevokedAtUtc == null && s.Grant.ExpiresAtUtc > now, ct);
    }

    public async Task RecordViewAsync(int sessionId, string path, CancellationToken ct = default)
    {
        var s = await db.SupportSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return;
        s.PageViews++;
        audit.Add("SupportSession.View", nameof(SupportSession), sessionId.ToString(), path.Length > 300 ? path[..300] : path);
        await db.SaveChangesAsync(ct);
    }

    public async Task EndSessionAsync(int sessionId, CancellationToken ct = default)
    {
        var s = await db.SupportSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null || s.EndedAtUtc is not null) return;
        s.EndedAtUtc = clock.UtcNow;
        audit.Add("SupportSession.End", nameof(SupportSession), sessionId.ToString(), $"{s.PageViews} page(s) viewed");
        await db.SaveChangesAsync(ct);
    }
}

// ====================================================================== announcements & maintenance

public sealed class AnnouncementInput
{
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Body { get; set; } = string.Empty;
    public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;
    [Display(Name = "Audience")] public int? PlanId { get; set; }
    [Required, Display(Name = "Show from")] public DateTime StartsAt { get; set; }
    [Display(Name = "Show until")] public DateTime? EndsAt { get; set; }
    [Display(Name = "Published")] public bool IsActive { get; set; } = true;
    [Display(Name = "Users can dismiss it")] public bool Dismissible { get; set; } = true;
}

public sealed record ActiveAnnouncement(int Id, string Title, string Body, AnnouncementSeverity Severity, bool Dismissible);

public sealed class MaintenanceInput
{
    [Display(Name = "Maintenance mode")] public bool Enabled { get; set; }
    [StringLength(500), Display(Name = "Message shown to users")] public string? Message { get; set; }
    [Display(Name = "Expected back (IST)")] public DateTime? Until { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "Support inbox (new-ticket emails)")] public string? SupportEmail { get; set; }
}

public sealed record MaintenanceState(bool Enabled, string? Message, DateTime? UntilUtc);

public interface IAnnouncementService
{
    Task<IReadOnlyList<PlatformAnnouncement>> ListAsync(CancellationToken ct = default);
    Task<AnnouncementInput?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> SaveAsync(int? id, AnnouncementInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ActiveAnnouncement>> ActiveForOrganizationAsync(int organizationId, CancellationToken ct = default);

    Task<MaintenanceState> MaintenanceAsync(CancellationToken ct = default);
    Task<MaintenanceInput> GetSystemSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSystemSettingsAsync(MaintenanceInput input, CancellationToken ct = default);
}

public sealed class AnnouncementService(IAppDbContext db, ICurrentContext context, IClock clock, IMemoryCache cache, IAuditWriter audit) : IAnnouncementService
{
    private const string AnnouncementsKey = "cnx:announcements";
    private const string MaintenanceKey = "cnx:maintenance";
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private void EnsurePlatform()
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Platform principal required.");
    }

    public async Task<IReadOnlyList<PlatformAnnouncement>> ListAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        return await db.PlatformAnnouncements.AsNoTracking().OrderByDescending(a => a.StartsAtUtc).ToListAsync(ct);
    }

    public async Task<AnnouncementInput?> GetAsync(int id, CancellationToken ct = default)
    {
        EnsurePlatform();
        var a = await db.PlatformAnnouncements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : new AnnouncementInput
        {
            Title = a.Title, Body = a.Body, Severity = a.Severity, PlanId = a.PlanId, StartsAt = a.StartsAtUtc + IstOffset,
            EndsAt = a.EndsAtUtc + IstOffset, IsActive = a.IsActive, Dismissible = a.Dismissible
        };
    }

    public async Task<Result> SaveAsync(int? id, AnnouncementInput i, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (i.EndsAt is { } end && end <= i.StartsAt) return Result.Fail("The end must be after the start.");
        if (i.PlanId is int p && !await db.SubscriptionPlans.AnyAsync(x => x.Id == p, ct)) return Result.Fail("Plan not found.");
        var a = id is null ? new PlatformAnnouncement() : await db.PlatformAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Fail("Announcement not found.");
        a.Title = i.Title.Trim(); a.Body = i.Body.Trim(); a.Severity = i.Severity; a.PlanId = i.PlanId;
        // Times are entered in IST (the platform's business time zone) and stored in UTC.
        a.StartsAtUtc = DateTime.SpecifyKind(i.StartsAt - IstOffset, DateTimeKind.Utc);
        a.EndsAtUtc = i.EndsAt is { } e ? DateTime.SpecifyKind(e - IstOffset, DateTimeKind.Utc) : null;
        a.IsActive = i.IsActive;
        a.Dismissible = i.Severity != AnnouncementSeverity.Critical && i.Dismissible;
        if (id is null) db.PlatformAnnouncements.Add(a);
        await db.SaveChangesAsync(ct);
        cache.Remove(AnnouncementsKey);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        EnsurePlatform();
        var a = await db.PlatformAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Fail("Announcement not found.");
        db.PlatformAnnouncements.Remove(a);
        await db.SaveChangesAsync(ct);
        cache.Remove(AnnouncementsKey);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ActiveAnnouncement>> ActiveForOrganizationAsync(int organizationId, CancellationToken ct = default)
    {
        var all = await cache.GetOrCreateAsync(AnnouncementsKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return await db.PlatformAnnouncements.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct);
        }) ?? [];
        var now = clock.UtcNow;
        var current = all.Where(a => a.StartsAtUtc <= now && (a.EndsAtUtc is null || a.EndsAtUtc > now)).ToList();
        if (current.Count == 0) return [];
        int? planId = null;
        if (current.Any(a => a.PlanId is not null))
            planId = await db.OrganizationSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.IsCurrent).Select(s => (int?)s.PlanId).FirstOrDefaultAsync(ct);
        return current.Where(a => a.PlanId is null || a.PlanId == planId)
            .OrderByDescending(a => a.Severity).ThenByDescending(a => a.StartsAtUtc)
            .Select(a => new ActiveAnnouncement(a.Id, a.Title, a.Body, a.Severity, a.Dismissible)).ToList();
    }

    public async Task<MaintenanceState> MaintenanceAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(MaintenanceKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            return new MaintenanceState(s?.MaintenanceEnabled == true, s?.MaintenanceMessage, s?.MaintenanceUntilUtc);
        }) ?? new MaintenanceState(false, null, null);

    public async Task<MaintenanceInput> GetSystemSettingsAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return new MaintenanceInput { Enabled = s?.MaintenanceEnabled == true, Message = s?.MaintenanceMessage, Until = s?.MaintenanceUntilUtc + IstOffset, SupportEmail = s?.SupportEmail };
    }

    public async Task<Result> SaveSystemSettingsAsync(MaintenanceInput i, CancellationToken ct = default)
    {
        EnsurePlatform();
        var s = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (s is null) { s = new Domain.Billing.PlatformSetting(); db.PlatformSettings.Add(s); }
        var changed = s.MaintenanceEnabled != i.Enabled;
        s.MaintenanceEnabled = i.Enabled;
        s.MaintenanceMessage = string.IsNullOrWhiteSpace(i.Message) ? null : i.Message.Trim();
        s.MaintenanceUntilUtc = i.Until is { } u ? DateTime.SpecifyKind(u - IstOffset, DateTimeKind.Utc) : null;
        s.SupportEmail = string.IsNullOrWhiteSpace(i.SupportEmail) ? null : i.SupportEmail.Trim();
        if (changed) audit.Add(i.Enabled ? "Maintenance.On" : "Maintenance.Off", "Platform", null, s.MaintenanceMessage);
        await db.SaveChangesAsync(ct);
        cache.Remove(MaintenanceKey);
        return Result.Ok();
    }
}
