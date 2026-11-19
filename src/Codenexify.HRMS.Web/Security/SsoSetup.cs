using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Codenexify.HRMS.Web.Security;

/// <summary>
/// Google and Microsoft sign-in (OpenID Connect, authorization code + PKCE). The app registrations belong to
/// Codenexify and are entered in the Control Center; they are read per request, so no restart is needed.
/// The identity provider only proves the email address — the HRMS account must already exist (see AlternateSignInService).
/// </summary>
public static partial class SsoSetup
{
    public const string ExternalScheme = "SsoExternal";
    public const string GoogleScheme = "sso-google";
    public const string MicrosoftScheme = "sso-microsoft";
    /// <summary>Required at startup by the handler; replaced with the real client id before every use.</summary>
    private const string Placeholder = "not-configured";

    [GeneratedRegex("^https://login\\.microsoftonline\\.com/([0-9a-fA-F-]{36})/v2\\.0$")]
    private static partial Regex MicrosoftIssuer();

    public static AuthenticationBuilder AddHrmsSso(this AuthenticationBuilder builder, CookieSecurePolicy secure)
    {
        builder.AddCookie(ExternalScheme, o =>
        {
            o.Cookie.Name = "cnx.sso";
            o.Cookie.Path = "/sso";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = secure;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
        builder.AddOpenIdConnect(GoogleScheme, o => Configure(o, "https://accounts.google.com", "/sso/google/callback", microsoft: false, secure));
        builder.AddOpenIdConnect(MicrosoftScheme, o => Configure(o, "https://login.microsoftonline.com/organizations/v2.0", "/sso/microsoft/callback", microsoft: true, secure));
        return builder;
    }

    private static void Configure(OpenIdConnectOptions o, string authority, string callback, bool microsoft, CookieSecurePolicy secure)
    {
        o.SignInScheme = ExternalScheme;
        o.Authority = authority;
        o.CallbackPath = callback;
        o.ClientId = Placeholder;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.ResponseMode = OpenIdConnectResponseMode.Query;
        o.UsePkce = true;
        o.SaveTokens = false;
        o.GetClaimsFromUserInfoEndpoint = false;
        o.MapInboundClaims = false;
        o.Scope.Clear();
        o.Scope.Add("openid");
        o.Scope.Add("email");
        o.Scope.Add("profile");
        o.CorrelationCookie.SecurePolicy = secure;
        o.NonceCookie.SecurePolicy = secure;
        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.NonceCookie.SameSite = SameSiteMode.Lax;
        o.TokenValidationParameters.NameClaimType = "name";
        if (microsoft)
        {
            // Multi-tenant: any work/school tenant may sign in; the issuer must name the same tenant as the token's tid.
            o.TokenValidationParameters.IssuerValidator = (issuer, token, _) =>
            {
                var m = MicrosoftIssuer().Match(issuer);
                var tid = (token as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken)?.GetPayloadValue<string>("tid");
                if (m.Success && string.Equals(m.Groups[1].Value, tid, StringComparison.OrdinalIgnoreCase)) return issuer;
                throw new SecurityTokenInvalidIssuerException("Unexpected Microsoft issuer.");
            };
        }

        o.Events.OnRedirectToIdentityProvider = async ctx =>
        {
            if (!await ApplyCredentialsAsync(ctx.HttpContext, ctx.Options, microsoft))
            {
                ctx.Response.Redirect("/login?sso=unavailable");
                ctx.HandleResponse();
                return;
            }
            ctx.ProtocolMessage.ClientId = ctx.Options.ClientId;
            if (microsoft) ctx.ProtocolMessage.Prompt = "select_account";
        };
        o.Events.OnMessageReceived = async ctx => await ApplyCredentialsAsync(ctx.HttpContext, ctx.Options, microsoft);
        o.Events.OnAuthorizationCodeReceived = async ctx =>
        {
            await ApplyCredentialsAsync(ctx.HttpContext, ctx.Options, microsoft);
            ctx.TokenEndpointRequest!.ClientId = ctx.Options.ClientId;
            ctx.TokenEndpointRequest.ClientSecret = ctx.Options.ClientSecret;
        };
        o.Events.OnRemoteFailure = ctx =>
        {
            ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Sso").LogWarning(ctx.Failure, "Single sign-on failed.");
            ctx.Response.Redirect("/login?sso=failed");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    }

    /// <summary>Copies the platform's client id/secret onto the (shared) handler options. Same values for every request, so this is race-safe.</summary>
    private static async Task<bool> ApplyCredentialsAsync(HttpContext http, OpenIdConnectOptions options, bool microsoft)
    {
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(http.RequestAborted);
        var id = microsoft ? s?.MicrosoftClientId : s?.GoogleClientId;
        var secret = microsoft ? s?.MicrosoftClientSecretProtected : s?.GoogleClientSecretProtected;
        if (id is null || secret is null) return false;
        options.ClientId = id;
        options.ClientSecret = http.RequestServices.GetRequiredService<ISecretProtector>().Unprotect(secret);
        return true;
    }
}
