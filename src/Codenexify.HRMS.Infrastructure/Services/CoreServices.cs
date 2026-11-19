using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Codenexify.HRMS.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Ist = FindIst();
    public DateTime UtcNow => DateTime.UtcNow;
    /// <summary>Business "today" in India Standard Time (subscriptions are dated in IST).</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist));

    private static TimeZoneInfo FindIst()
    {
        foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
    }
}

/// <summary>PBKDF2 hashing via ASP.NET Core Identity's PasswordHasher (V3 format, salted, iterated).</summary>
public sealed class PasswordService : IPasswordService
{
    private sealed class Principal;
    private readonly PasswordHasher<Principal> _hasher = new();
    private static readonly Principal Subject = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public (bool Verified, bool NeedsRehash) Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return (false, false);
        try
        {
            var r = _hasher.VerifyHashedPassword(Subject, hash, password);
            return (r != PasswordVerificationResult.Failed, r == PasswordVerificationResult.SuccessRehashNeeded);
        }
        catch (FormatException)
        {
            return (false, false);
        }
    }
}

/// <summary>RFC 6238 TOTP (SHA-1, 6 digits, 30 s step, ±1 step drift) compatible with common authenticator apps.</summary>
public sealed class TotpService : ITotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public bool Verify(string base32Secret, string code, DateTime utcNow)
    {
        code = (code ?? string.Empty).Replace(" ", string.Empty);
        if (code.Length != 6 || !code.All(char.IsDigit)) return false;
        var key = FromBase32(base32Secret);
        var step = new DateTimeOffset(utcNow, TimeSpan.Zero).ToUnixTimeSeconds() / 30;
        for (var drift = -1; drift <= 1; drift++)
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Compute(key, step + drift)), Encoding.ASCII.GetBytes(code)))
                return true;
        return false;
    }

    public string BuildProvisioningUri(string issuer, string account, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";

    public static string Compute(byte[] key, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    public static byte[] FromBase32(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException("Invalid base32 secret.");
            buffer = (buffer << 5) | val;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Codenexify.HRMS.Secrets.v1");
    public string Protect(string plain) => _protector.Protect(plain);
    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

/// <summary>Queues named business actions into the same unit of work as the data change.</summary>
public sealed class AuditWriter(AppDbContext db, ICurrentContext context, IClock clock) : IAuditWriter
{
    public void Add(string action, string entityType, string? entityId, string? details, int? organizationId = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = clock.UtcNow,
            OrganizationId = organizationId ?? context.OrganizationId,
            ActorType = context.PrincipalType,
            ActorId = context.UserId,
            ActorName = context.UserName ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is { Length: > 1000 } ? details[..1000] : details,
            IpAddress = context.IpAddress
        });
    }
}

/// <summary>
/// Base implementation of <see cref="ICurrentContext"/> with server-side scope overrides.
/// The web layer supplies the authenticated principal; tests and jobs supply fixed values.
/// </summary>
public abstract class CurrentContextBase : ICurrentContext
{
    private readonly Stack<(bool Platform, int? OrgId)> _overrides = new();

    protected abstract PrincipalType? AuthenticatedType { get; }
    protected abstract int? AuthenticatedOrganizationId { get; }
    public abstract int? UserId { get; }
    public abstract string? UserName { get; }
    public abstract string? IpAddress { get; }

    public PrincipalType? PrincipalType => AuthenticatedType;

    public bool IsPlatform => _overrides.Count > 0 ? _overrides.Peek().Platform : AuthenticatedType == Domain.Enums.PrincipalType.Platform;

    public int? OrganizationId => _overrides.Count > 0
        ? _overrides.Peek().OrgId
        : AuthenticatedType == Domain.Enums.PrincipalType.Tenant ? AuthenticatedOrganizationId : null;

    public IDisposable BeginOrganizationScope(int organizationId)
    {
        if (AuthenticatedType == Domain.Enums.PrincipalType.Tenant && AuthenticatedOrganizationId != organizationId)
            throw new Application.Common.TenantIsolationException("A tenant principal cannot enter another organization's scope.");
        _overrides.Push((false, organizationId));
        return new Pop(_overrides);
    }

    public IDisposable BeginPlatformScope()
    {
        if (AuthenticatedType == Domain.Enums.PrincipalType.Tenant)
            throw new Application.Common.TenantIsolationException("A tenant principal cannot enter platform scope.");
        _overrides.Push((true, null));
        return new Pop(_overrides);
    }

    private sealed class Pop(Stack<(bool, int?)> stack) : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            stack.Pop();
        }
    }
}
