using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class BillingTests : IAsyncLifetime
{
    private const string KeySecret = "test_key_secret";
    private const string WebhookSecret = "test_webhook_secret";
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.CreateAsync();
    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<T> AsPlatform<T>(Func<IBillingService, Task<T>> action)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => action(sp.GetRequiredService<IBillingService>()));
    }

    private Task<T> AsTenant<T>(int orgId, Func<IBillingService, Task<T>> action)
    {
        _host.Context.AsTenant(orgId);
        return _host.RunAsync(sp => action(sp.GetRequiredService<IBillingService>()));
    }

    private async Task EnableRazorpayAsync()
    {
        var settings = await AsPlatform(b => b.GetSettingsAsync());
        settings.RazorpayEnabled = true;
        settings.RazorpayKeyId = "rzp_test_key";
        settings.RazorpayKeySecret = KeySecret;
        settings.RazorpayWebhookSecret = WebhookSecret;
        Assert.True((await AsPlatform(b => b.SaveSettingsAsync(settings))).Succeeded);
    }

    private Task<Invoice> InvoiceAsync(int id)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == id));
    }

    private Task<Domain.Platform.OrganizationSubscription> CurrentSubscriptionAsync(int orgId)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().OrganizationSubscriptions.SingleAsync(s => s.OrganizationId == orgId && s.IsCurrent));
    }

    private static string Webhook(string evt, string orderId, string paymentId) =>
        System.Text.Json.JsonSerializer.Serialize(new { @event = evt, payload = new { payment = new { entity = new { id = paymentId, order_id = orderId, status = "captured" } } } });

    // ------------------------------------------------------------------ pure helpers

    [Fact]
    public void Gst_is_split_into_cgst_sgst_within_the_state_and_igst_across_states()
    {
        var intra = GstCalculator.Split(1000m, 18m, "27", "27");
        Assert.Equal((9m, 90m, 9m, 90m, 0m, 0m, 1180m), (intra.CgstRate, intra.Cgst, intra.SgstRate, intra.Sgst, intra.IgstRate, intra.Igst, intra.Total));

        var inter = GstCalculator.Split(99m, 18m, "27", "29");
        Assert.Equal((0m, 0m, 18m, 17.82m, 116.82m), (inter.Cgst, inter.Sgst, inter.IgstRate, inter.Igst, inter.Total));

        Assert.Equal("29", GstStates.CodeFor("29ABCDE1234F1Z5", "Maharashtra")); // GSTIN wins over the address
        Assert.Equal("27", GstStates.CodeFor(null, "maharashtra"));
        Assert.Null(GstStates.CodeFor(null, "Atlantis"));
    }

    [Fact]
    public void Razorpay_signatures_are_verified_with_hmac_sha256()
    {
        var sig = RazorpaySignature.Compute("order_1|pay_1", KeySecret);
        Assert.True(RazorpaySignature.VerifyPayment("order_1", "pay_1", sig, KeySecret));
        Assert.True(RazorpaySignature.VerifyPayment("order_1", "pay_1", sig.ToUpperInvariant(), KeySecret));
        Assert.False(RazorpaySignature.VerifyPayment("order_1", "pay_2", sig, KeySecret));
        Assert.False(RazorpaySignature.VerifyPayment("order_1", "pay_1", sig, "other_secret"));
        Assert.False(RazorpaySignature.VerifyPayment("order_1", "pay_1", "", KeySecret));
        Assert.True(RazorpaySignature.VerifyWebhook("{}", RazorpaySignature.Compute("{}", WebhookSecret), WebhookSecret));
    }

    // ------------------------------------------------------------------ invoices

    [Fact]
    public async Task Invoices_are_numbered_per_financial_year_with_gst()
    {
        var org = await _host.ProvisionAsync("numbr");
        var plan = await _host.PlanIdAsync("FOUNDATION");
        var start = _host.Clock.Today;

        var first = await AsPlatform(b => b.CreateSubscriptionInvoiceAsync(org, InvoicePurpose.NewSubscription, plan, BillingCycle.Monthly, null, start));
        var second = await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Onboarding", Quantity = 2, UnitPrice = 500 }));
        Assert.True(first.Succeeded, first.Error);
        Assert.True(second.Succeeded, second.Error);

        var a = await InvoiceAsync(first.Value);
        var b2 = await InvoiceAsync(second.Value);
        Assert.Equal("CNX/26-27/00001", a.Number); // clock is 15 Jun 2026 → FY 2026-27
        Assert.Equal("CNX/26-27/00002", b2.Number);
        // Organization has no GSTIN/state → treated as the supplier's state: CGST + SGST.
        Assert.Equal((99m, 8.91m, 8.91m, 0m, 116.82m), (a.Subtotal, a.CgstAmount, a.SgstAmount, a.IgstAmount, a.Total));
        Assert.Equal(1180m, b2.Total);
        Assert.Equal(start.AddMonths(1).AddDays(-1), a.PeriodEnd);
        Assert.Equal(2, _host.Email.Sent.Count(m => m.Category == "invoice"));

        // Same plan and period cannot be invoiced twice.
        Assert.False((await AsPlatform(b => b.CreateSubscriptionInvoiceAsync(org, InvoicePurpose.NewSubscription, plan, BillingCycle.Monthly, null, start))).Succeeded);

        // A new financial year restarts the series.
        _host.Clock.UtcNow = new DateTime(2027, 4, 2, 6, 0, 0, DateTimeKind.Utc);
        var next = await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Training", Quantity = 1, UnitPrice = 100 }));
        Assert.Equal("CNX/27-28/00001", (await InvoiceAsync(next.Value)).Number);
    }

    [Fact]
    public async Task Inter_state_customer_is_charged_igst()
    {
        var org = await _host.ProvisionAsync("karna");
        _host.Context.AsPlatform();
        await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var o = await db.Organizations.SingleAsync(x => x.Id == org);
            o.Gstin = "29ABCDE1234F1Z5";
            o.State = "Karnataka";
            await db.SaveChangesAsync();
        });
        var r = await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Setup", Quantity = 1, UnitPrice = 1000 }));
        var inv = await InvoiceAsync(r.Value);
        Assert.Equal((0m, 0m, 180m, 1180m), (inv.CgstAmount, inv.SgstAmount, inv.IgstAmount, inv.Total));
        Assert.Equal("Karnataka (29)", inv.PlaceOfSupply);
    }

    [Fact]
    public async Task Invoice_with_payments_cannot_be_cancelled()
    {
        var org = await _host.ProvisionAsync("cancl");
        var id = (await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "X", Quantity = 1, UnitPrice = 100 }))).Value;
        Assert.True((await AsPlatform(b => b.RecordOfflinePaymentAsync(id, new OfflinePaymentInput { Amount = 50, Reference = "UTR1", ReceivedOn = _host.Clock.Today }))).Succeeded);
        Assert.Equal(InvoiceStatus.PartiallyPaid, (await InvoiceAsync(id)).Status);
        Assert.False((await AsPlatform(b => b.CancelInvoiceAsync(id, "Wrong"))).Succeeded);

        var other = (await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Y", Quantity = 1, UnitPrice = 100 }))).Value;
        Assert.True((await AsPlatform(b => b.CancelInvoiceAsync(other, "Raised in error"))).Succeeded);
        Assert.Equal(InvoiceStatus.Cancelled, (await InvoiceAsync(other)).Status);
        Assert.False((await AsPlatform(b => b.RecordOfflinePaymentAsync(other, new OfflinePaymentInput { Amount = 10, Reference = "U", ReceivedOn = _host.Clock.Today }))).Succeeded);
    }

    // ------------------------------------------------------------------ online purchase

    [Fact]
    public async Task Tenant_buys_a_plan_online_and_subscription_is_activated_once()
    {
        await EnableRazorpayAsync();
        var org = await _host.ProvisionAsync("buyer", trial: true);
        var growth = await _host.PlanIdAsync("GROWTH");

        var purchase = await AsTenant(org, b => b.CreatePurchaseInvoiceAsync(growth, BillingCycle.Yearly));
        Assert.True(purchase.Succeeded, purchase.Error);
        // Asking again reuses the pending invoice instead of creating another.
        Assert.Equal(purchase.Value, (await AsTenant(org, b => b.CreatePurchaseInvoiceAsync(growth, BillingCycle.Yearly))).Value);

        var checkout = await AsTenant(org, b => b.StartOnlinePaymentAsync(purchase.Value, org));
        Assert.True(checkout.Succeeded, checkout.Error);
        var invoice = await InvoiceAsync(purchase.Value);
        Assert.Equal((long)(invoice.Total * 100), checkout.Value!.AmountPaise);

        // A forged signature is rejected and nothing is paid.
        Assert.False((await AsTenant(org, b => b.ConfirmOnlinePaymentAsync(checkout.Value.OrderId, "pay_1", "deadbeef", org))).Succeeded);
        Assert.Equal(0m, (await InvoiceAsync(purchase.Value)).AmountPaid);

        var sig = RazorpaySignature.Compute($"{checkout.Value.OrderId}|pay_1", KeySecret);
        Assert.True((await AsTenant(org, b => b.ConfirmOnlinePaymentAsync(checkout.Value.OrderId, "pay_1", sig, org))).Succeeded);
        // The webhook for the same payment arrives too: it must not double count.
        var body = Webhook("payment.captured", checkout.Value.OrderId, "pay_1");
        _host.Context.AsAnonymous();
        using (_host.Context.BeginPlatformScope())
            Assert.True((await _host.RunAsync(sp => sp.GetRequiredService<IBillingService>().HandleWebhookAsync(body, RazorpaySignature.Compute(body, WebhookSecret)))).Succeeded);

        var paid = await InvoiceAsync(purchase.Value);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.Equal(paid.Total, paid.AmountPaid);
        Assert.True(paid.Fulfilled);
        var sub = await CurrentSubscriptionAsync(org);
        Assert.Equal((growth, SubscriptionStatus.Active, BillingCycle.Yearly, false), (sub.PlanId, sub.Status, sub.BillingCycle, sub.IsTrial));

        // A paid subscriber cannot buy again through self-service.
        Assert.False((await AsTenant(org, b => b.CreatePurchaseInvoiceAsync(growth, BillingCycle.Monthly))).Succeeded);
    }

    [Fact]
    public async Task Webhook_alone_captures_payment_and_rejects_bad_signatures()
    {
        await EnableRazorpayAsync();
        var org = await _host.ProvisionAsync("hookd");
        var id = (await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Support", Quantity = 1, UnitPrice = 1000 }))).Value;
        var order = (await AsTenant(org, b => b.StartOnlinePaymentAsync(id, org))).Value!.OrderId;
        var body = Webhook("payment.captured", order, "pay_hook");

        _host.Context.AsAnonymous();
        using (_host.Context.BeginPlatformScope())
        {
            var billing = (Func<IServiceProvider, IBillingService>)(sp => sp.GetRequiredService<IBillingService>());
            Assert.False((await _host.RunAsync(sp => billing(sp).HandleWebhookAsync(body, "bad"))).Succeeded);
            Assert.True((await _host.RunAsync(sp => billing(sp).HandleWebhookAsync(body, RazorpaySignature.Compute(body, WebhookSecret)))).Succeeded);
            Assert.True((await _host.RunAsync(sp => billing(sp).HandleWebhookAsync(body, RazorpaySignature.Compute(body, WebhookSecret)))).Succeeded);
        }
        var inv = await InvoiceAsync(id);
        Assert.Equal((InvoiceStatus.Paid, 1180m), (inv.Status, inv.AmountPaid));
        Assert.Single(inv.Payments, p => p.Status == PaymentStatus.Captured && p.GatewayPaymentId == "pay_hook");
    }

    // ------------------------------------------------------------------ credit notes

    [Fact]
    public async Task Credit_note_with_refund_goes_through_the_gateway()
    {
        await EnableRazorpayAsync();
        var org = await _host.ProvisionAsync("refnd");
        var id = (await AsPlatform(b => b.CreateManualInvoiceAsync(org, new ManualInvoiceInput { Description = "Setup", Quantity = 1, UnitPrice = 1000 }))).Value;
        var order = (await AsTenant(org, b => b.StartOnlinePaymentAsync(id, org))).Value!.OrderId;
        Assert.True((await AsTenant(org, b => b.ConfirmOnlinePaymentAsync(order, "pay_r", RazorpaySignature.Compute($"{order}|pay_r", KeySecret), org))).Succeeded);
        var paymentId = (await InvoiceAsync(id)).Payments.Single().Id;

        // Without a refund the credit can only cover what is unpaid (nothing here).
        Assert.False((await AsPlatform(b => b.IssueCreditNoteAsync(id, new CreditNoteInput { Amount = 100, Reason = "Goodwill" }))).Succeeded);

        var r = await AsPlatform(b => b.IssueCreditNoteAsync(id, new CreditNoteInput { Amount = 590, Reason = "Half refund", Refund = true, PaymentId = paymentId }));
        Assert.True(r.Succeeded, r.Error);
        Assert.Equal(("pay_r", 590m), _host.Gateway.Refunds.Single());

        var inv = await InvoiceAsync(id);
        Assert.Equal((1180m, 590m, 590m, 0m), (inv.Total, inv.AmountPaid, inv.AmountCredited, inv.Balance));
        Assert.Equal(PaymentStatus.PartiallyRefunded, inv.Payments.Single().Status);
        var note = await AsPlatform(b => b.CreditNotesForAsync(id));
        Assert.Equal(("CNXCN/26-27/00001", 500m, 45m, 45m), (note.Single().Number, note.Single().TaxableAmount, note.Single().CgstAmount, note.Single().SgstAmount));

        // Cannot refund more than what remains on the payment.
        Assert.False((await AsPlatform(b => b.IssueCreditNoteAsync(id, new CreditNoteInput { Amount = 600, Reason = "Too much", Refund = true, PaymentId = paymentId }))).Succeeded);
    }

    // ------------------------------------------------------------------ isolation

    [Fact]
    public async Task Tenants_only_see_their_own_invoices()
    {
        var a = await _host.ProvisionAsync("tena");
        var b = await _host.ProvisionAsync("tenb");
        var invA = (await AsPlatform(x => x.CreateManualInvoiceAsync(a, new ManualInvoiceInput { Description = "A", Quantity = 1, UnitPrice = 10 }))).Value;

        Assert.Empty((await AsTenant(b, x => x.InvoicesAsync(new InvoiceQuery { OrganizationId = a }))).Items);
        Assert.Single((await AsTenant(a, x => x.InvoicesAsync(new InvoiceQuery()))).Items);
        Assert.Null(await AsTenant(b, x => x.GetInvoiceAsync(invA, b)));
        await Assert.ThrowsAsync<TenantIsolationException>(() => AsTenant(b, x => x.GetInvoiceAsync(invA, a)));
        Assert.False((await AsTenant(b, x => x.StartOnlinePaymentAsync(invA, b))).Succeeded);
        // Platform-only operations refuse tenant principals.
        await Assert.ThrowsAsync<TenantIsolationException>(() => AsTenant(b, x => x.RecordOfflinePaymentAsync(invA, new OfflinePaymentInput { Amount = 1, Reference = "x", ReceivedOn = _host.Clock.Today })));
    }

    // ------------------------------------------------------------------ automation

    [Fact]
    public async Task Billing_job_raises_renewal_once_sends_reminders_and_renewal_payment_extends_subscription()
    {
        var org = await _host.ProvisionAsync("renew", trial: false);
        _host.Context.AsPlatform();
        var plan = await _host.PlanIdAsync("FOUNDATION");
        Assert.True((await _host.RunAsync(sp => sp.GetRequiredService<ISubscriptionService>().ActivateAsync(org,
            new ActivateSubscriptionInput { PlanId = plan, BillingCycle = BillingCycle.Monthly, Price = 80, StartDate = _host.Clock.Today }))).Succeeded);
        var end = (await CurrentSubscriptionAsync(org)).EndDate;

        Task<BillingAutomationResult> Run()
        {
            _host.Context.AsPlatform();
            return _host.RunAsync(sp => sp.GetRequiredService<IBillingAutomation>().RunAsync());
        }

        Assert.Equal(0, (await Run()).RenewalInvoices); // too early
        _host.Clock.UtcNow = end.AddDays(-5).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        Assert.Equal(1, (await Run()).RenewalInvoices);
        Assert.Equal(0, (await Run()).RenewalInvoices); // idempotent

        var renewal = (await AsPlatform(b => b.InvoicesAsync(new InvoiceQuery { OrganizationId = org }))).Items.Single(i => i.Purpose == InvoicePurpose.Renewal);
        var inv = await InvoiceAsync(renewal.Id);
        Assert.Equal((80m, end.AddDays(1)), (inv.Subtotal, inv.PeriodStart!.Value)); // agreed price, next period

        // Due in 7 days by default; the schedule "3,1,0,-3,-7" sends on day due-3, once.
        _host.Clock.UtcNow = inv.DueDate.AddDays(-3).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        Assert.Equal(1, (await Run()).Reminders);
        Assert.Equal(0, (await Run()).Reminders);
        _host.Clock.UtcNow = inv.DueDate.AddDays(-2).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        Assert.Equal(0, (await Run()).Reminders); // not a scheduled day

        Assert.True((await AsPlatform(b => b.RecordOfflinePaymentAsync(inv.Id, new OfflinePaymentInput { Amount = inv.Total, Reference = "NEFT123", ReceivedOn = _host.Clock.Today }))).Succeeded);
        Assert.True((await InvoiceAsync(inv.Id)).Fulfilled);
        Assert.Equal(end.AddMonths(1), (await CurrentSubscriptionAsync(org)).EndDate);
    }

    [Fact]
    public async Task Trial_ending_notice_is_sent_once_per_day()
    {
        var org = await _host.ProvisionAsync("trial", trial: true);
        var trialEnd = (await CurrentSubscriptionAsync(org)).TrialEndsOn!.Value;
        _host.Clock.UtcNow = trialEnd.AddDays(-3).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        _host.Context.AsPlatform();

        // The fake sender does not write EmailLogs, so record the notice the way the SMTP sender would.
        var first = await _host.RunAsync(sp => sp.GetRequiredService<IBillingAutomation>().RunAsync());
        Assert.Equal(1, first.TrialNotices);
        await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.EmailLogs.Add(new EmailLog { CreatedAtUtc = _host.Clock.UtcNow, OrganizationId = org, To = "x", Subject = "s", Category = "trial-ending", Succeeded = true });
            await db.SaveChangesAsync();
        });
        Assert.Equal(0, (await _host.RunAsync(sp => sp.GetRequiredService<IBillingAutomation>().RunAsync())).TrialNotices);
        Assert.Contains(_host.Email.Sent, m => m.Category == "trial-ending" && m.OrganizationId == org);
    }
}
