using Argus.Web.Data;
using Argus.Web.Data.Seeding;
using Argus.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Argus.Web.Tests;

/// <summary>Real ASP.NET Core Identity on the in-memory SQLite database: seeding and operator management.</summary>
public class IdentityTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private UserManager<IdentityUser> _users = null!;
    private OperatorService _operators = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySeed:AdminPassword"] = "Test-Admin-1",
            ["IdentitySeed:OperatorPassword"] = "Test-Operator-1",
        }).Build());
        services.AddDbContext<ArgusDbContext>(options => options.UseSqlite(_connection));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ArgusDbContext>();
        services.AddScoped<IdentitySeeder>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateAsyncScope();

        await _scope.ServiceProvider.GetRequiredService<ArgusDbContext>().Database.EnsureCreatedAsync();
        await _scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        _operators = new OperatorService(_users);
    }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Seed_creates_one_admin_and_one_operator_and_a_second_run_changes_nothing()
    {
        await _scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(); // like the next app start

        Assert.Equal([("argus.admin", ArgusRoles.Admin), ("argus.operator", ArgusRoles.Operator)],
            (await _operators.GetAllAsync()).Select(account => (account.UserName, account.Role)));
        Assert.True(await _users.CheckPasswordAsync((await _users.FindByNameAsync("argus.admin"))!, "Test-Admin-1"));
    }

    [Fact]
    public async Task Admin_creates_and_deletes_operators_but_never_an_admin()
    {
        Assert.True((await _operators.CreateOperatorAsync(" night.shift ", "Night-Shift-1")).Succeeded);
        var nightShift = (await _users.FindByNameAsync("night.shift"))!;
        Assert.True(await _users.IsInRoleAsync(nightShift, ArgusRoles.Operator));

        Assert.True(await _operators.DeleteOperatorAsync(nightShift.Id));
        Assert.Null(await _users.FindByNameAsync("night.shift"));

        var admin = (await _users.FindByNameAsync("argus.admin"))!;
        Assert.False(await _operators.DeleteOperatorAsync(admin.Id));
        Assert.False(await _operators.DeleteOperatorAsync("no-such-id"));
    }

    [Theory]
    [InlineData("argus.operator", "Another-Pass-1")] // name taken
    [InlineData("weak.one", "password")]             // Identity's rules: needs upper case, a digit and a symbol
    public async Task Operator_is_refused_with_identitys_reason(string userName, string password)
    {
        var result = await _operators.CreateOperatorAsync(userName, password);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
    }
}