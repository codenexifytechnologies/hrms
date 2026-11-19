using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Codenexify.HRMS.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace Codenexify.HRMS.Web.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "codenexify-hrms";
    public string Audience { get; set; } = "codenexify-hrms-api";
    public string Key { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;
}

public static class AuthSetup
{
    private static readonly TimeSpan RevalidateEvery = TimeSpan.FromMinutes(5);
    private const string ValidatedAtKey = "cnx.validated";

    public static IServiceCollection AddHrmsAuthentication(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters (use an environment variable or user secrets).");
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));

        var secure = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddAuthentication(Schemes.Smart)
            .AddPolicyScheme(Schemes.Smart, "Select by path", o => o.ForwardDefaultSelector = ctx =>
            {
                var path = ctx.Request.Path;
                if (path.StartsWithSegments("/superadmin")) return Schemes.Platform;
                if (path.StartsWithSegments("/api")) return Schemes.Bearer;
                return Schemes.Tenant;
            })
            .AddCookie(Schemes.Platform, o =>
            {
                o.Cookie.Name = "cnx.platform";
                o.Cookie.Path = "/superadmin"; // the platform cookie is never sent to tenant URLs
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = secure;
                o.LoginPath = "/superadmin/account/login";
                o.LogoutPath = "/superadmin/account/logout";
                o.AccessDeniedPath = "/superadmin/account/denied";
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                o.Events.OnValidatePrincipal = ValidatePlatformAsync;
            })
            .AddCookie(Schemes.Tenant, o =>
            {
                o.Cookie.Name = "cnx.tenant";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = secure;
                o.LoginPath = "/login";
                o.LogoutPath = "/logout";
                o.AccessDeniedPath = "/denied";
                o.ExpireTimeSpan = TimeSpan.FromHours(10);
                o.SlidingExpiration = true;
                o.Events.OnValidatePrincipal = ValidateTenantAsync;
            })
            .AddJwtBearer(Schemes.Bearer, o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            })
            .AddHrmsSso(secure);

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Platform, p => p
                .AddAuthenticationSchemes(Schemes.Platform)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimNames.PrincipalType, ClaimNames.PlatformValue))
            .AddPolicy(Policies.Tenant, p => p
                .AddAuthenticationSchemes(Schemes.Tenant)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimNames.PrincipalType, ClaimNames.TenantValue)
                .RequireClaim(ClaimNames.OrganizationId))
            .AddPolicy(Policies.TenantApi, p => p
                .AddAuthenticationSchemes(Schemes.Bearer)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimNames.PrincipalType, ClaimNames.TenantValue)
                .RequireClaim(ClaimNames.OrganizationId));

        services.AddSingleton<TokenService>();
        return services;
    }

    private static bool DueForRevalidation(CookieValidatePrincipalContext ctx)
    {
        var raw = ctx.Properties.GetString(ValidatedAtKey);
        return !DateTimeOffset.TryParse(raw, out var at) || DateTimeOffset.UtcNow - at > RevalidateEvery;
    }

    /// <summary>Re-reads the account periodically so disabled users / changed permissions take effect quickly.</summary>
    private static async Task ValidatePlatformAsync(CookieValidatePrincipalContext ctx)
    {
        if (!DueForRevalidation(ctx)) return;
        var principal = ctx.Principal!;
        var id = principal.GetUserId();
        var stamp = principal.FindFirstValue(ClaimNames.SecurityStamp);
        var auth = ctx.HttpContext.RequestServices.GetRequiredService<IPlatformAuthService>();
        var fresh = id is null || stamp is null ? null : await auth.RefreshAsync(id.Value, stamp);
        if (fresh is null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(Schemes.Platform);
            return;
        }
        ctx.ReplacePrincipal(PrincipalFactory.Create(fresh, Schemes.Platform));
        ctx.Properties.SetString(ValidatedAtKey, DateTimeOffset.UtcNow.ToString("O"));
        ctx.ShouldRenew = true;
    }

    private static async Task ValidateTenantAsync(CookieValidatePrincipalContext ctx)
    {
        // Support sessions have no tenant user; SupportSessionMiddleware checks them on every request.
        if (ctx.Principal?.HasClaim(c => c.Type == ClaimNames.SupportSession) == true) return;
        if (!DueForRevalidation(ctx)) return;
        var principal = ctx.Principal!;
        var id = principal.GetUserId();
        var org = principal.GetOrganizationId();
        var stamp = principal.FindFirstValue(ClaimNames.SecurityStamp);
        var auth = ctx.HttpContext.RequestServices.GetRequiredService<ITenantAuthService>();
        var fresh = id is null || org is null || stamp is null ? null : await auth.RefreshAsync(org.Value, id.Value, stamp);
        if (fresh is null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(Schemes.Tenant);
            return;
        }
        ctx.ReplacePrincipal(PrincipalFactory.Create(fresh, Schemes.Tenant));
        ctx.Properties.SetString(ValidatedAtKey, DateTimeOffset.UtcNow.ToString("O"));
        ctx.ShouldRenew = true;
    }

    public static AuthenticationProperties NewProperties(bool persistent) => new()
    {
        IsPersistent = persistent,
        Items = { [ValidatedAtKey] = DateTimeOffset.UtcNow.ToString("O") }
    };
}

/// <summary>Issues short-lived JWTs for tenant users (mobile apps / integrations).</summary>
public sealed class TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresUtc) Issue(AuthenticatedPrincipal principal)
    {
        var o = options.Value;
        var identity = PrincipalFactory.Create(principal, Schemes.Bearer);
        var expires = DateTime.UtcNow.AddMinutes(o.LifetimeMinutes);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, identity.Claims, DateTime.UtcNow, expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
