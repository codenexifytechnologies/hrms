namespace Codenexify.HRMS.Web.Security;

/// <summary>
/// The site address put into emailed links. Uses <c>Platform:PublicUrl</c>; only in Development does it fall back to
/// the request's own host (in production a forged Host header could otherwise point reset links at another site).
/// </summary>
public static class PublicUrl
{
    public static string? For(HttpRequest request, IConfiguration config, IHostEnvironment env)
    {
        var configured = config["Platform:PublicUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim().TrimEnd('/');
        return env.IsDevelopment() ? $"{request.Scheme}://{request.Host}" : null;
    }
}
