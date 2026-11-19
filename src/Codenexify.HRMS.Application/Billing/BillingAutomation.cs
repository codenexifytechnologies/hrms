using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Billing;

public sealed record BillingAutomationResult(int RenewalInvoices, int Reminders, int TrialNotices);

public interface IBillingAutomation
{
    /// <summary>Raises renewal invoices, sends payment reminders and trial-ending notices. Safe to run repeatedly (idempotent per day).</summary>
    Task<BillingAutomationResult> RunAsync(CancellationToken ct = default);
}

public sealed class BillingAutomation(IAppDbContext db, ICurrentContext context, IClock clock, IBillingService billing, IEmailSender email) : IBillingAutomation
{
    /// <summary>Trial-ending notices go out this many days before the trial ends.</summary>
    private static readonly int[] TrialNoticeDays = [3, 1];

    public async Task<BillingAutomationResult> RunAsync(CancellationToken ct = default)
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Billing automation runs in platform scope.");
        var settings = await billing.SupplierAsync(ct);
        var today = clock.Today;

        // 1. Renewal invoices for paid subscriptions that end soon.
        var horizon = today.AddDays(settings.RenewalInvoiceDaysBefore);
        var due = await db.OrganizationSubscriptions.AsNoTracking()
            .Where(s => s.IsCurrent && s.AutoRenew && s.TrialEndsOn == null
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
                && s.EndDate <= horizon && !s.Organization.IsDeleted)
            .Select(s => new { s.OrganizationId, s.PlanId, s.BillingCycle, s.Price, s.EndDate })
            .ToListAsync(ct);
        var renewals = 0;
        foreach (var s in due)
        {
            var periodStart = s.EndDate.AddDays(1);
            if (await db.Invoices.AnyAsync(i => i.OrganizationId == s.OrganizationId && i.PeriodStart == periodStart && i.Status != InvoiceStatus.Cancelled, ct)) continue;
            var r = await billing.CreateSubscriptionInvoiceAsync(s.OrganizationId, InvoicePurpose.Renewal, s.PlanId, s.BillingCycle, s.Price > 0 ? s.Price : null, periodStart, ct);
            if (r.Succeeded) renewals++;
        }

        // 2. Payment reminders on the configured schedule (days before due; negative = overdue).
        var schedule = settings.ReminderSchedule.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var d) ? d : (int?)null).Where(d => d.HasValue).Select(d => d!.Value).ToHashSet();
        var open = await db.Invoices.Include(i => i.Organization)
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid).ToListAsync(ct);
        var reminders = 0;
        var dayStartUtc = clock.UtcNow.AddHours(-20); // at most one reminder per invoice per day
        foreach (var inv in open)
        {
            var daysToDue = inv.DueDate.DayNumber - today.DayNumber;
            if (!schedule.Contains(daysToDue) || inv.Balance <= 0) continue;
            if (inv.LastReminderAtUtc is { } last && last > dayStartUtc) continue;
            await email.SendAsync(new EmailMessage(inv.Organization.Email,
                daysToDue >= 0 ? $"Reminder: invoice {inv.Number} due {inv.DueDate:dd MMM yyyy}" : $"Overdue: invoice {inv.Number}",
                BillingEmails.Reminder(inv, inv.Organization.Name, daysToDue), "reminder", inv.OrganizationId), ct);
            inv.LastReminderAtUtc = clock.UtcNow;
            reminders++;
        }
        await db.SaveChangesAsync(ct);

        // 3. Trial-ending notices (deduplicated through the email log).
        var trials = await db.OrganizationSubscriptions.AsNoTracking()
            .Where(s => s.IsCurrent && s.TrialEndsOn != null && s.Status == SubscriptionStatus.Trial && !s.Organization.IsDeleted)
            .Select(s => new { s.OrganizationId, TrialEndsOn = s.TrialEndsOn!.Value, s.Organization.Email, s.Organization.Name })
            .ToListAsync(ct);
        var notices = 0;
        foreach (var t in trials.Where(t => TrialNoticeDays.Contains(t.TrialEndsOn.DayNumber - today.DayNumber)))
        {
            if (await db.EmailLogs.AnyAsync(l => l.OrganizationId == t.OrganizationId && l.Category == "trial-ending" && l.CreatedAtUtc > dayStartUtc, ct)) continue;
            await email.SendAsync(new EmailMessage(t.Email, $"Your trial ends on {t.TrialEndsOn:dd MMM yyyy}", BillingEmails.TrialEnding(t.Name, t.TrialEndsOn), "trial-ending", t.OrganizationId), ct);
            notices++;
        }
        return new BillingAutomationResult(renewals, reminders, notices);
    }
}
