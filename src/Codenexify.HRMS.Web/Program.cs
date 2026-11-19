using System.Threading.RateLimiting;
using Codenexify.HRMS.Application;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Infrastructure;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(config, runBackgroundJobs: config.GetValue("BackgroundJobs:Enabled", true));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentContext>();
builder.Services.AddScoped<ICurrentContext>(sp => sp.GetRequiredService<HttpCurrentContext>());

// Keys persist on disk so cookies and protected secrets (TOTP keys) survive restarts / app-pool recycles.
var keysPath = config["DataProtection:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
builder.Services.AddDataProtection().SetApplicationName("Codenexify.HRMS").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddHrmsAuthentication(config, builder.Environment);

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<MustChangePasswordFilter>();
})
// API clients get enum names ("Announcement"), not numbers; numbers are still accepted on input.
.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "cnx.af";
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // New hires' secure welcome link (no sign-in): enough for a form and several uploads, too few to guess tokens.
    o.AddPolicy("preboarding", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// Database:SeedOnStartup=false is only for hosts that build and seed the database themselves (integration tests).
if (config.GetValue("Database:SeedOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (config.GetValue("Database:AutoMigrate", true))
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        // DBAs deploy the schema with the scripts in /database; refuse to run against an older schema.
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
            throw new InvalidOperationException(
                $"The database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. " +
                "Run database/000_FullDatabase.sql (or the numbered scripts) against it, or set Database__AutoMigrate=true.");
    }
    // Starting data (features, permissions, plans, statutory rates, first Super Admin, policy drafts). Data only, idempotent.
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Location only on self-service (clock-in) and HR's office-location setup pages.
    var geo = ctx.Request.Path.StartsWithSegments("/me") || ctx.Request.Path.StartsWithSegments("/org/attendance/locations");
    // Camera only on self-service pages (selfie clock-in).
    var camera = ctx.Request.Path.StartsWithSegments("/me");
    h["Permissions-Policy"] = $"camera={(camera ? "(self)" : "()")}, microphone=(), geolocation={(geo ? "(self)" : "()")}";
    // Only the organization payment page may load Razorpay Checkout (its script, frames and API calls).
    var checkout = ctx.Request.Path.StartsWithSegments("/org/billing");
    var rzpScript = checkout ? " https://checkout.razorpay.com" : "";
    var rzpFrame = checkout ? "frame-src https://api.razorpay.com https://checkout.razorpay.com; " : "";
    var rzpConnect = checkout ? " https://api.razorpay.com https://lumberjack.razorpay.com" : "";
    h["Content-Security-Policy"] =
        $"default-src 'self'; script-src 'self' https://cdn.jsdelivr.net https://code.jquery.com{rzpScript}; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; img-src 'self' data: https:; " +
        $"{rzpFrame}connect-src 'self'{rzpConnect}; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    await next();
});

var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    // Let browsers keep CSS/JS/images instead of asking again on every page. Files linked with asp-append-version
    // carry ?v=<content hash>, so they can be cached for a year: a changed file gets a new URL.
    OnPrepareResponse = ctx =>
    {
        var versioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = versioned ? "public, max-age=31536000, immutable" : "public, max-age=604800";
    }
});
app.UseMiddleware<Codenexify.HRMS.Web.Security.MaintenanceMiddleware>();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<Codenexify.HRMS.Web.Security.SupportSessionMiddleware>();
app.UseAuthorization();

app.MapAreaControllerRoute("superadmin", "SuperAdmin", "superadmin/{controller=Dashboard}/{action=Index}/{id?}");
app.MapAreaControllerRoute("orgadmin", "OrgAdmin", "org/{controller=Dashboard}/{action=Index}/{id?}");
app.MapAreaControllerRoute("employee", "Employee", "me/{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
