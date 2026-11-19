using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Infrastructure.Persistence;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Codenexify.HRMS.Infrastructure.Services;

/// <summary>Razorpay Orders and Refunds API (https://api.razorpay.com/v1) with basic auth from platform settings.</summary>
public sealed class RazorpayGateway(IHttpClientFactory httpFactory, IServiceScopeFactory scopes, ISecretProtector protector, ILogger<RazorpayGateway> logger) : IPaymentGateway
{
    public const string HttpClientName = "razorpay";

    private async Task<HttpClient> ClientAsync(CancellationToken ct)
    {
        string keyId, secret;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var s = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (s is not { RazorpayEnabled: true, RazorpayKeyId: not null, RazorpayKeySecretProtected: not null })
                throw new InvalidOperationException("Razorpay is not configured.");
            keyId = s.RazorpayKeyId;
            secret = protector.Unprotect(s.RazorpayKeySecretProtected);
        }
        var client = httpFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{secret}")));
        return client;
    }

    private static long Paise(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    public async Task<GatewayOrder> CreateOrderAsync(decimal amount, string receipt, IReadOnlyDictionary<string, string> notes, CancellationToken ct = default)
    {
        using var client = await ClientAsync(ct);
        var response = await client.PostAsJsonAsync("orders", new { amount = Paise(amount), currency = "INR", receipt, notes }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Razorpay order creation failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Razorpay rejected the order.");
        }
        var order = JsonSerializer.Deserialize<OrderResponse>(body)!;
        return new GatewayOrder(order.Id, order.Amount, order.Currency);
    }

    public async Task<GatewayRefund> RefundAsync(string paymentId, decimal amount, string note, CancellationToken ct = default)
    {
        using var client = await ClientAsync(ct);
        var response = await client.PostAsJsonAsync($"payments/{Uri.EscapeDataString(paymentId)}/refund",
            new { amount = Paise(amount), notes = new Dictionary<string, string> { ["reason"] = note } }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Razorpay refund failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Razorpay rejected the refund.");
        }
        var refund = JsonSerializer.Deserialize<RefundResponse>(body)!;
        return new GatewayRefund(refund.Id, refund.Status);
    }

    private sealed record OrderResponse([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("amount")] long Amount, [property: JsonPropertyName("currency")] string Currency);
    private sealed record RefundResponse([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("status")] string Status);
}

/// <summary>SMTP delivery via MailKit using the platform settings. Every attempt is written to EmailLogs (own scope, so it never flushes the caller's changes).</summary>
public sealed class SmtpEmailSender(IServiceScopeFactory scopes, ISecretProtector protector, IClock clock, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        string? error = null;
        if (string.IsNullOrWhiteSpace(message.To)) error = "No recipient address.";
        else if (s is not { SmtpEnabled: true, SmtpHost: not null, SmtpFromEmail: not null }) error = "Email sending is turned off in platform settings.";
        else
        {
            try
            {
                var mime = new MimeMessage();
                mime.From.Add(new MailboxAddress(s.SmtpFromName, s.SmtpFromEmail));
                mime.To.Add(MailboxAddress.Parse(message.To));
                mime.Subject = message.Subject;
                mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();
                using var smtp = new SmtpClient { Timeout = 20000 };
                await smtp.ConnectAsync(s.SmtpHost, s.SmtpPort, s.SmtpUseSsl ? (s.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls) : SecureSocketOptions.None, ct);
                if (!string.IsNullOrEmpty(s.SmtpUsername) && s.SmtpPasswordProtected is not null)
                    await smtp.AuthenticateAsync(s.SmtpUsername, protector.Unprotect(s.SmtpPasswordProtected), ct);
                await smtp.SendAsync(mime, ct);
                await smtp.DisconnectAsync(true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email to {To} failed.", message.To);
                error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            }
        }
        db.EmailLogs.Add(new EmailLog
        {
            CreatedAtUtc = clock.UtcNow, OrganizationId = message.OrganizationId, To = message.To.Length > 200 ? message.To[..200] : message.To,
            Subject = message.Subject.Length > 300 ? message.Subject[..300] : message.Subject, Category = message.Category, Succeeded = error is null, Error = error
        });
        await db.SaveChangesAsync(ct);
        return error is null;
    }
}
