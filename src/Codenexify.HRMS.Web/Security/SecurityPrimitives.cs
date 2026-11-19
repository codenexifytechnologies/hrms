using System.Security.Claims;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Services;

namespace Codenexify.HRMS.Web.Security;

public static class Schemes
{
    public const string Platform = "PlatformCookie";
    public const string Tenant = "TenantCookie";
    public const string Bearer = "Bearer";
    public const string Smart = "Smart";
}

public static class Policies
{
    public const string Platform = "Platform";
    public const string Tenant = "Tenant";
    public const string TenantApi = "TenantApi";
}

public static class ClaimNames
{
    public const string PrincipalType = "cnx:pt";
    public const string OrganizationId = "cnx:org";
    public const string OrganizationCode = "cnx:orgcode";
    public const string OrganizationName = "cnx:orgname";
    public const string SecurityStamp = "cnx:stamp";
    public const string Permission = "cnx:perm";
    public const string MustChangePassword = "cnx:mcp";
    /// <summary>Present only on a Codenexify staff member's read-only support session (value = session id).</summary>
    public const string SupportSession = "cnx:support";
    public const string SupportUntil = "cnx:support-until";
    public const string PlatformValue = "platform";
    public const string TenantValue = "tenant";
}

public static class PrincipalFactory
{
    public static ClaimsPrincipal Create(AuthenticatedPrincipal p, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, p.UserId.ToString()),
            new(ClaimTypes.Name, p.FullName),
            new(ClaimTypes.Email, p.Email),
            new(ClaimNames.PrincipalType, p.Type == PrincipalType.Platform ? ClaimNames.PlatformValue : ClaimNames.TenantValue),
            new(ClaimNames.SecurityStamp, p.SecurityStamp)
        };
        if (p.Role is not null) claims.Add(new Claim(ClaimTypes.Role, p.Role));
        if (p.OrganizationId is { } org)
        {
            claims.Add(new Claim(ClaimNames.OrganizationId, org.ToString()));
            claims.Add(new Claim(ClaimNames.OrganizationCode, p.OrganizationCode ?? string.Empty));
            claims.Add(new Claim(ClaimNames.OrganizationName, p.OrganizationName ?? string.Empty));
        }
        if (p.MustChangePassword) claims.Add(new Claim(ClaimNames.MustChangePassword, "1"));
        claims.AddRange(p.Permissions.Select(perm => new Claim(ClaimNames.Permission, perm)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, ClaimTypes.Name, ClaimTypes.Role));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static int? GetOrganizationId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimNames.OrganizationId), out var id) ? id : null;

    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.HasClaim(ClaimNames.Permission, permission);

    public static bool IsPlatform(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(ClaimNames.PrincipalType, ClaimNames.PlatformValue);

    public static bool IsTenant(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(ClaimNames.PrincipalType, ClaimNames.TenantValue);

    public static int? GetSupportSessionId(this ClaimsPrincipal user) =>
        user.IsTenant() && int.TryParse(user.FindFirstValue(ClaimNames.SupportSession), out var id) ? id : null;
}

/// <summary>Current execution context derived exclusively from the authenticated principal.</summary>
public sealed class HttpCurrentContext(IHttpContextAccessor accessor) : CurrentContextBase
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    protected override PrincipalType? AuthenticatedType =>
        User is null ? null
        : User.IsPlatform() ? Domain.Enums.PrincipalType.Platform
        : User.IsTenant() ? Domain.Enums.PrincipalType.Tenant
        : null;

    protected override int? AuthenticatedOrganizationId => User?.IsTenant() == true ? User.GetOrganizationId() : null;
    public override int? UserId => AuthenticatedType is null ? null : User?.GetUserId();
    public override string? UserName => AuthenticatedType is null ? null : User?.FindFirstValue(ClaimTypes.Email);
    public override string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
