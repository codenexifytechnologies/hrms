using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Codenexify.HRMS.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` tooling (migrations, scripts). The connection string comes from the web app's own
/// configuration, exactly as at run time: src/Codenexify.HRMS.Web/appsettings.json, then
/// appsettings.{ASPNETCORE_ENVIRONMENT, default Development}.json, then environment variables
/// (ConnectionStrings__Default). Nothing is hard-coded here.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var webFolder = FindWebProjectFolder();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var config = new ConfigurationBuilder()
            .SetBasePath(webFolder)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException($"ConnectionStrings:Default is not set in {Path.Combine(webFolder, "appsettings.json")} (or appsettings.{environment}.json).");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options;
        return new AppDbContext(options, new NoPrincipalContext(), new SystemClock());
    }

    /// <summary>Looks upward from the working folder for src/Codenexify.HRMS.Web (works from the repo root or any project folder).</summary>
    private static string FindWebProjectFolder()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                foreach (var candidate in new[] { Path.Combine(dir.FullName, "src", "Codenexify.HRMS.Web"), Path.Combine(dir.FullName, "Codenexify.HRMS.Web") })
                    if (File.Exists(Path.Combine(candidate, "appsettings.json"))) return candidate;
            }
        }
        throw new InvalidOperationException("Could not find src/Codenexify.HRMS.Web/appsettings.json. Run dotnet ef from inside the repository.");
    }

    private sealed class NoPrincipalContext : CurrentContextBase
    {
        protected override PrincipalType? AuthenticatedType => null;
        protected override int? AuthenticatedOrganizationId => null;
        public override int? UserId => null;
        public override string? UserName => null;
        public override string? IpAddress => null;
    }
}
