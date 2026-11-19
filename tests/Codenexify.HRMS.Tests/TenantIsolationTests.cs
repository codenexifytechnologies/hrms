using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Tenancy;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Proves one organization can never read or change another organization's records.</summary>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _orgA, _orgB;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _orgA = await _host.ProvisionAsync("alpha");
        _orgB = await _host.ProvisionAsync("bravo");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<int> UserIdOf(int orgId)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.Where(u => u.OrganizationId == orgId).Select(u => u.Id).FirstAsync());
    }

    [Fact]
    public async Task Tenant_sees_only_its_own_users_and_roles()
    {
        _host.Context.AsTenant(_orgA);
        var (users, roles) = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (await db.Users.ToListAsync(), await db.Roles.ToListAsync());
        });

        Assert.NotEmpty(users);
        Assert.All(users, u => Assert.Equal(_orgA, u.OrganizationId));
        Assert.NotEmpty(roles);
        Assert.All(roles, r => Assert.Equal(_orgA, r.OrganizationId));
    }

    [Fact]
    public async Task Tenant_cannot_load_another_tenants_record_by_id()
    {
        var bUserId = await UserIdOf(_orgB);
        _host.Context.AsTenant(_orgA);

        var found = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.FirstOrDefaultAsync(u => u.Id == bUserId));

        Assert.Null(found);
    }

    [Fact]
    public async Task Tenant_cannot_update_another_tenants_record_even_when_attached_directly()
    {
        var bUserId = await UserIdOf(_orgB);
        _host.Context.AsPlatform();
        var bUser = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.AsNoTracking().FirstAsync(u => u.Id == bUserId));

        _host.Context.AsTenant(_orgA);
        await Assert.ThrowsAsync<TenantIsolationException>(() => _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            bUser.FullName = "Hijacked";
            db.Users.Update(bUser);
            await db.SaveChangesAsync();
        }));

        _host.Context.AsPlatform();
        var reloaded = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.FirstAsync(u => u.Id == bUserId));
        Assert.NotEqual("Hijacked", reloaded.FullName);
    }

    [Fact]
    public async Task Tenant_cannot_delete_another_tenants_record()
    {
        var bUserId = await UserIdOf(_orgB);
        _host.Context.AsPlatform();
        var bUser = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.AsNoTracking().FirstAsync(u => u.Id == bUserId));

        _host.Context.AsTenant(_orgA);
        await Assert.ThrowsAsync<TenantIsolationException>(() => _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Users.Remove(bUser);
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Tenant_cannot_create_records_for_another_tenant()
    {
        _host.Context.AsTenant(_orgA);
        await Assert.ThrowsAsync<TenantIsolationException>(() => _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Roles.Add(new Role { OrganizationId = _orgB, Name = "Injected" });
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task New_tenant_rows_are_stamped_with_the_authenticated_organization()
    {
        _host.Context.AsTenant(_orgA);
        var role = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var r = new Role { Name = "Auditors" }; // no OrganizationId supplied by caller
            db.Roles.Add(r);
            await db.SaveChangesAsync();
            return r;
        });
        Assert.Equal(_orgA, role.OrganizationId);
    }

    [Fact]
    public async Task Anonymous_context_sees_no_tenant_data_and_cannot_write()
    {
        _host.Context.AsAnonymous();
        var count = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.CountAsync());
        Assert.Equal(0, count);

        await Assert.ThrowsAsync<TenantIsolationException>(() => _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Roles.Add(new Role { OrganizationId = _orgA, Name = "Anon" });
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Platform_context_sees_all_tenants()
    {
        _host.Context.AsPlatform();
        var orgs = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.Select(u => u.OrganizationId).Distinct().ToListAsync());
        Assert.Contains(_orgA, orgs);
        Assert.Contains(_orgB, orgs);
    }

    [Fact]
    public void Tenant_principal_cannot_elevate_into_another_organization_or_platform_scope()
    {
        _host.Context.AsTenant(_orgA);
        Assert.Throws<TenantIsolationException>(() => _host.Context.BeginOrganizationScope(_orgB));
        Assert.Throws<TenantIsolationException>(() => _host.Context.BeginPlatformScope());
    }

    [Fact]
    public async Task Org_admin_service_rejects_other_tenants_user_and_role_ids()
    {
        var bUserId = await UserIdOf(_orgB);
        _host.Context.AsPlatform();
        var bRoleIds = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Roles.Where(r => r.OrganizationId == _orgB).Select(r => r.Id).ToListAsync());

        _host.Context.AsTenant(_orgA);
        var update = await _host.RunAsync(sp => sp.GetRequiredService<ITenantAdminService>().UpdateUserAsync(bUserId,
            new TenantUserInput { FullName = "X", Email = "x@x.test", RoleIds = bRoleIds }));
        Assert.False(update.Succeeded);
        Assert.Equal("User not found.", update.Error);

        var create = await _host.RunAsync(sp => sp.GetRequiredService<ITenantAdminService>().CreateUserAsync(
            new TenantUserInput { FullName = "Sneaky", Email = "sneaky@alpha.test", Password = "Passw0rd!x", RoleIds = bRoleIds }));
        Assert.False(create.Succeeded); // B's roles are invisible to A, so no valid role remains
    }

    [Fact]
    public async Task Soft_deleted_rows_are_hidden()
    {
        _host.Context.AsTenant(_orgA);
        var roleId = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var r = new Role { Name = "Temp" };
            db.Roles.Add(r);
            await db.SaveChangesAsync();
            db.Roles.Remove(r);
            await db.SaveChangesAsync();
            return r.Id;
        });

        var visible = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Roles.AnyAsync(r => r.Id == roleId));
        var physical = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == roleId && r.IsDeleted));
        Assert.False(visible);
        Assert.True(physical);
    }

    [Fact]
    public async Task Data_changes_are_written_to_the_audit_trail_without_secrets()
    {
        _host.Context.AsPlatform();
        var logs = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().AuditLogs
            .Where(a => a.EntityType == nameof(User) && a.OrganizationId == _orgA).ToListAsync());
        Assert.NotEmpty(logs);
        Assert.All(logs, l => Assert.DoesNotContain("PasswordHash", l.NewValues ?? string.Empty));
    }
}
