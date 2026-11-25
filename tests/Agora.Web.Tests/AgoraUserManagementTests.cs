using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraUserManagementTests
{
    [Fact]
    public async Task Search_ban_unban_and_audit_are_persisted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.UserAsync("admin", "Admin User", "Admin");
        var member = await fixture.UserAsync("member", "Maya Member", "Member");
        var page = await fixture.Service.GetPageAsync(" Maya ", 1);
        Assert.Single(page.Users); Assert.Equal(member.Id, page.Users[0].Id);

        await fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, true, "Repeated abuse");
        Assert.True((await fixture.Context.Users.FindAsync(member.Id))!.IsBanned);
        var audit = await fixture.Context.UserBanActions.SingleAsync();
        Assert.True(audit.IsBan); Assert.Equal("Repeated abuse", audit.Reason);
        await Assert.ThrowsAsync<BannedUserException>(() => fixture.BanStatus.EnsureNotBannedAsync(member.Id));

        await fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, false, "Appeal accepted");
        Assert.False((await fixture.Context.Users.FindAsync(member.Id))!.IsBanned);
        Assert.Equal(2, await fixture.Context.UserBanActions.CountAsync());
    }

    [Fact]
    public async Task Ban_rules_reject_self_admin_moderator_and_invalid_state_changes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.UserAsync("admin", "Admin", "Admin");
        var moderator = await fixture.UserAsync("moderator", "Moderator", "Moderator");
        var member = await fixture.UserAsync("member", "Member", "Member");
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeBanAsync(admin.Id, moderator.Id, false, true, "Reason"));
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeBanAsync(moderator.Id, moderator.Id, false, true, "Reason"));
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeBanAsync(member.Id, moderator.Id, false, true, " "));
        await fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, true, "Reason");
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, true, "Reason"));
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, false, new string('x', 501)));
        await fixture.Service.ChangeBanAsync(member.Id, admin.Id, true, false, new string('x', 500));
    }

    [Fact]
    public async Task Admin_role_changes_preserve_member_role_and_reject_self_demotion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.UserAsync("admin", "Admin", "Admin");
        var member = await fixture.UserAsync("member", "Member", "Member");
        await fixture.Service.ChangeModeratorRoleAsync(member.Id, admin.Id, true);
        Assert.True(await fixture.Users.IsInRoleAsync(member, "Moderator")); Assert.True(await fixture.Users.IsInRoleAsync(member, "Member"));
        await fixture.Service.ChangeModeratorRoleAsync(member.Id, admin.Id, false);
        Assert.False(await fixture.Users.IsInRoleAsync(member, "Moderator"));
        await fixture.Users.AddToRoleAsync(admin, "Moderator");
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeModeratorRoleAsync(admin.Id, admin.Id, false));
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeModeratorRoleAsync(admin.Id, member.Id, true));
        await Assert.ThrowsAsync<UserManagementException>(() => fixture.Service.ChangeModeratorRoleAsync(admin.Id, member.Id, false));
    }

    [Fact]
    public void Action_visibility_policy_matches_admin_moderator_and_member_protections()
    {
        var admin = new Agora.Web.Models.Moderation.ManagedUserViewModel { Id = "admin", Roles = ["Admin"] };
        var moderator = new Agora.Web.Models.Moderation.ManagedUserViewModel { Id = "moderator", Roles = ["Moderator"] };
        var member = new Agora.Web.Models.Moderation.ManagedUserViewModel { Id = "member", Roles = ["Member"] };

        Assert.False(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeBan(admin, "actor", true));
        Assert.False(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeModeratorRole(admin, true));
        Assert.False(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeBan(moderator, "moderator", true));
        Assert.False(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeBan(moderator, "other-moderator", false));
        Assert.True(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeBan(moderator, "admin", true));
        Assert.True(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeBan(member, "moderator", false));
        Assert.False(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeModeratorRole(member, false));
        Assert.True(Agora.Web.Models.Moderation.UserManagementActionPolicy.CanChangeModeratorRole(member, true));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider; private readonly IServiceScope scope;
        private Fixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; scope = provider.CreateScope(); Context = scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); Service = scope.ServiceProvider.GetRequiredService<IUserManagementService>(); BanStatus = scope.ServiceProvider.GetRequiredService<IBanStatusService>(); }
        public AgoraDbContext Context { get; } public UserManager<ApplicationUser> Users { get; } public IUserManagementService Service { get; } public IBanStatusService BanStatus { get; }
        public static async Task<Fixture> CreateAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(o => o.UseSqlite(connection)); services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AgoraDbContext>(); services.AddScoped<IUserManagementRepository, UserManagementRepository>(); services.AddScoped<IUserManagementService, UserManagementService>(); services.AddScoped<IBanStatusService, BanStatusService>(); var fixture = new Fixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); foreach (var role in new[] { "Admin", "Moderator", "Member" }) await fixture.Context.Roles.AddAsync(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() }); await fixture.Context.SaveChangesAsync(); return fixture; }
        public async Task<ApplicationUser> UserAsync(string name, string display, string role) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = display }; Assert.True((await Users.CreateAsync(user, "TestPassword!1")).Succeeded); Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded); return user; }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
