using System.Net.Http.Json;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Services;

/// <summary>
/// Sends one-time codes through the MSG91 Flow API using the DLT-approved template configured in the Control Center.
/// Credentials live (encrypted) in platform settings, so nothing is hard-coded.
/// </summary>
public sealed class Msg91SmsSender(IHttpClientFactory httpFactory, IServiceScopeFactory scopes, ISecretProtector protector, ILogger<Msg91SmsSender> logger) : ISmsSender
{
    public const string HttpClientName = "msg91";

    private async Task<(string Key, string Template, string Variable)?> SettingsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var s = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is not { SmsEnabled: true, Msg91AuthKeyProtected: not null, Msg91TemplateId: not null }) return null;
        return (protector.Unprotect(s.Msg91AuthKeyProtected), s.Msg91TemplateId, s.Msg91OtpVariable);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default) => await SettingsAsync(ct) is not null;

    public async Task<bool> SendOtpAsync(string mobile, string code, CancellationToken ct = default)
    {
        if (await SettingsAsync(ct) is not var (key, template, variable)) return false;
        try
        {
            var http = httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "flow/");
            request.Headers.Add("authkey", key);
            var recipient = new Dictionary<string, string> { ["mobiles"] = "91" + mobile, [variable] = code };
            request.Content = JsonContent.Create(new Dictionary<string, object> { ["template_id"] = template, ["short_url"] = "0", ["recipients"] = new[] { recipient } });
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;
            // The body may echo the template but never the code variable value we log here.
            logger.LogWarning("MSG91 rejected an OTP SMS: HTTP {Status}.", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "MSG91 OTP SMS could not be sent.");
            return false;
        }
    }
}
