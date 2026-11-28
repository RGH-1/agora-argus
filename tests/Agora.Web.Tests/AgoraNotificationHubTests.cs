using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Agora.Web.Tests;

public sealed class AgoraNotificationHubTests
{
    [Fact]
    public void Hub_is_authorized_and_exposes_no_client_group_subscription_methods()
    {
        Assert.NotEmpty(typeof(AgoraNotificationHub).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.DoesNotContain(typeof(AgoraNotificationHub).GetMethods(), method => method.DeclaringType == typeof(AgoraNotificationHub) && method.Name != nameof(AgoraNotificationHub.OnConnectedAsync));
    }

    [Fact]
    public async Task Member_joins_only_authenticated_private_group()
    {
        await using var fixture = await Fixture.CreateAsync(); var member = await fixture.UserAsync("member");
        var groups = await fixture.ConnectAsync(member.Id);
        Assert.Equal(["user:" + member.Id], groups.Added);
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Privileged_roles_join_private_and_moderators_groups(string role)
    {
        await using var fixture = await Fixture.CreateAsync(); var user = await fixture.UserAsync(role.ToLowerInvariant(), role);
        var groups = await fixture.ConnectAsync(user.Id);
        Assert.Equal(["user:" + user.Id, "moderators"], groups.Added);
    }

    [Fact]
    public async Task Anonymous_banned_and_deleted_principals_are_aborted_without_groups()
    {
        await using var fixture = await Fixture.CreateAsync(); var banned = await fixture.UserAsync("banned", banned: true);
        var anonymous = await fixture.ConnectAsync(null); var bannedConnection = await fixture.ConnectAsync(banned.Id); var deleted = await fixture.ConnectAsync("deleted-user");
        Assert.True(anonymous.Aborted); Assert.Empty(anonymous.Added);
        Assert.True(bannedConnection.Aborted); Assert.Empty(bannedConnection.Added);
        Assert.True(deleted.Aborted); Assert.Empty(deleted.Added);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider;
        private Fixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; Context = provider.GetRequiredService<AgoraDbContext>(); Users = provider.GetRequiredService<UserManager<ApplicationUser>>(); }
        public AgoraDbContext Context { get; } public UserManager<ApplicationUser> Users { get; }
        public static async Task<Fixture> CreateAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var services = new ServiceCollection(); services.AddLogging(); services.AddDbContext<AgoraDbContext>(o => o.UseSqlite(connection)); services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AgoraDbContext>(); var provider = services.BuildServiceProvider(); var fixture = new Fixture(connection, provider); await fixture.Context.Database.EnsureCreatedAsync(); foreach (var role in new[] { "Member", "Moderator", "Admin" }) fixture.Context.Roles.Add(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() }); await fixture.Context.SaveChangesAsync(); return fixture; }
        public async Task<ApplicationUser> UserAsync(string name, string? role = null, bool banned = false) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name, IsBanned = banned }; Assert.True((await Users.CreateAsync(user)).Succeeded); if (role is not null) Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded); return user; }
        public async Task<FakeGroups> ConnectAsync(string? userId) { var context = new FakeContext(userId); var groups = new FakeGroups(); var hub = new AgoraNotificationHub(provider.GetRequiredService<IServiceScopeFactory>(), Users) { Context = context, Groups = groups }; await hub.OnConnectedAsync(); groups.Aborted = context.Aborted; return groups; }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class FakeGroups : IGroupManager { public List<string> Added { get; } = []; public bool Aborted { get; set; } public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) { Added.Add(groupName); return Task.CompletedTask; } public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class FakeContext : HubCallerContext
    {
        public FakeContext(string? userId) { User = userId is null ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test")); }
        public bool Aborted { get; private set; } public override string ConnectionId => "connection"; public override string? UserIdentifier => User.FindFirstValue(ClaimTypes.NameIdentifier); public override ClaimsPrincipal User { get; } public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>(); public override IFeatureCollection Features { get; } = new FeatureCollection(); public override CancellationToken ConnectionAborted => CancellationToken.None; public override void Abort() => Aborted = true;
    }
}
