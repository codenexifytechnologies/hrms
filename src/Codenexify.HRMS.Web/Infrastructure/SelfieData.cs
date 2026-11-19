namespace Codenexify.HRMS.Web;

/// <summary>Decodes a clock-in photo sent as a JPEG data URL (web) or plain base64 (mobile API).</summary>
public static class SelfieData
{
    public static byte[]? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        const string prefix = "data:image/jpeg;base64,";
        var b64 = value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;
        // Base64 of the 400 KB maximum is ~547 KB; anything longer is rejected before decoding.
        if (b64.Length > 560_000) return [];
        try { return Convert.FromBase64String(b64); }
        catch (FormatException) { return []; }
    }
}
