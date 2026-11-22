using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Data.Seeding;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraDatabaseTests
{
    [Fact]
    public async Task Model_configures_required_relationships_and_unique_like_index()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var model = fixture.Context.Model;
        var like = model.FindEntityType(typeof(Like))!;

        Assert.Contains(like.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Like.UserId), nameof(Like.PostId)]));
        Assert.Equal(DeleteBehavior.Cascade, model.FindEntityType(typeof(Post))!
            .GetForeignKeys().Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Forum)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, model.FindEntityType(typeof(Forum))!
            .GetForeignKeys().Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser)).DeleteBehavior);
    }

    [Fact]
    public async Task Duplicate_like_for_same_user_and_post_is_rejected()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, post) = await fixture.AddUserAndPostAsync();
        fixture.Context.Likes.Add(new Like { UserId = user.Id, PostId = post.Id });
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Likes.Add(new Like { UserId = user.Id, PostId = post.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Forum_and_post_persist_with_navigation_relationship()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, post) = await fixture.AddUserAndPostAsync();
        var persisted = await fixture.Context.Posts.Include(item => item.Forum)
            .Include(item => item.Author).SingleAsync();

        Assert.Equal(post.Id, persisted.Id);
        Assert.Equal("Test forum", persisted.Forum.Name);
        Assert.Equal(user.Id, persisted.AuthorId);
        Assert.False(persisted.IsRemoved);
    }

    [Fact]
    public async Task Development_seeder_creates_expected_data_idempotently()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var seeder = fixture.Services.GetRequiredService<DevelopmentDatabaseSeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(3, await fixture.Context.Roles.CountAsync());
        Assert.Equal(4, await fixture.Context.Users.CountAsync());
        Assert.Equal(4, await fixture.Context.Rules.CountAsync());
        var adminRole = await fixture.Context.Roles.SingleAsync(item => item.Name == "Admin");
        Assert.Equal(1, await fixture.Context.UserRoles.CountAsync(role => role.RoleId == adminRole.Id));
    }

    [Fact]
    public async Task Forum_service_creates_pending_forum_through_repository()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var user = new ApplicationUser { UserName = "creator", Email = "creator@agora.local", DisplayName = "Creator" };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var service = fixture.Services.GetRequiredService<IForumService>();
        var forum = await service.CreateAsync("Service forum", "Created by service", user.Id);

        var persisted = await fixture.Context.Forums.SingleAsync();
        Assert.Equal(forum.Id, persisted.Id);
        Assert.Equal(ForumStatus.Pending, persisted.Status);
        Assert.Equal(user.Id, persisted.CreatorId);
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private SqliteFixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
            _scope = provider.CreateScope();
            Services = _scope.ServiceProvider;
            Context = Services.GetRequiredService<AgoraDbContext>();
        }

        public IServiceProvider Services { get; }
        public AgoraDbContext Context { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var settings = new Dictionary<string, string?>
            {
                ["DevelopmentSeed:AdminPassword"] = "TestSeedPassword!1",
                ["DevelopmentSeed:ModeratorPassword"] = "TestSeedPassword!1",
                ["DevelopmentSeed:MemberPassword"] = "TestSeedPassword!1"
            };
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
            services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AgoraDbContext>();
            services.AddScoped<DevelopmentDatabaseSeeder>();
            services.AddScoped<IForumRepository, ForumRepository>();
            services.AddScoped<IForumService, ForumService>();
            var provider = services.BuildServiceProvider();
            var fixture = new SqliteFixture(connection, provider);
            await fixture.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<(ApplicationUser User, Post Post)> AddUserAndPostAsync()
        {
            var user = new ApplicationUser { UserName = "member", Email = "member@agora.local", DisplayName = "Member" };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();
            var forum = new Forum { Name = "Test forum", NormalizedName = "TEST FORUM", CreatorId = user.Id, Status = ForumStatus.Approved };
            Context.Forums.Add(forum);
            await Context.SaveChangesAsync();
            var post = new Post { ForumId = forum.Id, AuthorId = user.Id, Content = "A persisted test post." };
            Context.Posts.Add(post);
            await Context.SaveChangesAsync();
            return (user, post);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _scope.Dispose();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
