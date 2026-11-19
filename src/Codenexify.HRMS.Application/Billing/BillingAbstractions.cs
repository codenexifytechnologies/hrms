using System.Security.Cryptography;
using System.Text;

namespace Codenexify.HRMS.Application.Billing;

public sealed record GatewayOrder(string OrderId, long AmountPaise, string Currency);
public sealed record GatewayRefund(string RefundId, string Status);

/// <summary>Online payment gateway (Razorpay). Credentials are read from platform settings on each call.</summary>
public interface IPaymentGateway
{
    Task<GatewayOrder> CreateOrderAsync(decimal amount, string receipt, IReadOnlyDictionary<string, string> notes, CancellationToken ct = default);
    Task<GatewayRefund> RefundAsync(string paymentId, decimal amount, string note, CancellationToken ct = default);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string Category, int? OrganizationId = null);

public interface IEmailSender
{
    /// <summary>Sends (or records why it could not send) an email; never throws for delivery problems.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Razorpay signature checks (HMAC-SHA256, constant-time comparison).</summary>
public static class RazorpaySignature
{
    public static string Compute(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>Checkout callback: signature over "order_id|payment_id" with the key secret.</summary>
    public static bool VerifyPayment(string orderId, string paymentId, string signature, string keySecret) =>
        Matches(Compute($"{orderId}|{paymentId}", keySecret), signature);

    /// <summary>Webhook: signature over the raw request body with the webhook secret.</summary>
    public static bool VerifyWebhook(string body, string signature, string webhookSecret) =>
        Matches(Compute(body, webhookSecret), signature);

    private static bool Matches(string expected, string? actual) =>
        !string.IsNullOrEmpty(actual) && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual.Trim().ToLowerInvariant()));
}

/// <summary>GST state codes (first two digits of a GSTIN) used to decide intra- vs inter-state tax.</summary>
public static class GstStates
{
    public static readonly IReadOnlyDictionary<string, string> CodeByState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Jammu and Kashmir"] = "01", ["Himachal Pradesh"] = "02", ["Punjab"] = "03", ["Chandigarh"] = "04", ["Uttarakhand"] = "05",
        ["Haryana"] = "06", ["Delhi"] = "07", ["Rajasthan"] = "08", ["Uttar Pradesh"] = "09", ["Bihar"] = "10", ["Sikkim"] = "11",
        ["Arunachal Pradesh"] = "12", ["Nagaland"] = "13", ["Manipur"] = "14", ["Mizoram"] = "15", ["Tripura"] = "16", ["Meghalaya"] = "17",
        ["Assam"] = "18", ["West Bengal"] = "19", ["Jharkhand"] = "20", ["Odisha"] = "21", ["Chhattisgarh"] = "22", ["Madhya Pradesh"] = "23",
        ["Gujarat"] = "24", ["Dadra and Nagar Haveli and Daman and Diu"] = "26", ["Maharashtra"] = "27", ["Karnataka"] = "29", ["Goa"] = "30",
        ["Lakshadweep"] = "31", ["Kerala"] = "32", ["Tamil Nadu"] = "33", ["Puducherry"] = "34", ["Andaman and Nicobar Islands"] = "35",
        ["Telangana"] = "36", ["Andhra Pradesh"] = "37", ["Ladakh"] = "38"
    };

    public static string? CodeFor(string? gstin, string? state)
    {
        if (!string.IsNullOrWhiteSpace(gstin) && gstin.Length >= 2 && char.IsDigit(gstin[0]) && char.IsDigit(gstin[1])) return gstin[..2];
        return state is not null && CodeByState.TryGetValue(state.Trim(), out var code) ? code : null;
    }

    public static string? StateFor(string code) => CodeByState.FirstOrDefault(kv => kv.Value == code).Key;
}

public sealed record GstSplit(decimal CgstRate, decimal Cgst, decimal SgstRate, decimal Sgst, decimal IgstRate, decimal Igst, decimal Total);

public static class GstCalculator
{
    /// <summary>Intra-state supply → CGST + SGST (half each); inter-state → IGST. Rounded to paise.</summary>
    public static GstSplit Split(decimal taxable, decimal rate, string supplierStateCode, string placeOfSupplyCode)
    {
        if (supplierStateCode == placeOfSupplyCode)
        {
            var half = rate / 2;
            var c = Math.Round(taxable * half / 100, 2, MidpointRounding.AwayFromZero);
            return new GstSplit(half, c, half, c, 0, 0, taxable + 2 * c);
        }
        var i = Math.Round(taxable * rate / 100, 2, MidpointRounding.AwayFromZero);
        return new GstSplit(0, 0, 0, 0, rate, i, taxable + i);
    }
}
