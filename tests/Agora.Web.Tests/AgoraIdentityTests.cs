using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Agora.Web.Controllers;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agora.Web.Tests;

public class AgoraIdentityTests
{
    [Fact]
    public async Task Valid_registration_creates_member_without_role_selection()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var controller = fixture.CreateAccountController();
        var result = await controller.Register(new RegisterViewModel
        {
            UserName = "new.member", Email = "new.member@agora.local", DisplayName = "New Member",
            Password = "TestPassword!1", ConfirmPassword = "TestPassword!1"
        });

        var user = await fixture.UserManager.FindByNameAsync("new.member");
        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(user);
        Assert.True(await fixture.UserManager.IsInRoleAsync(user, "Member"));
        Assert.DoesNotContain(typeof(RegisterViewModel).GetProperties(), property => property.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Invalid_or_duplicate_registration_is_rejected()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var invalid = new RegisterViewModel { UserName = "bad", Email = "not-an-email", DisplayName = "Bad", Password = "one", ConfirmPassword = "two" };
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));

        var first = fixture.CreateAccountController();
        await first.Register(new RegisterViewModel { UserName = "duplicate", Email = "duplicate@agora.local", DisplayName = "First", Password = "TestPassword!1", ConfirmPassword = "TestPassword!1" });
        var second = fixture.CreateAccountController();
        var result = await second.Register(new RegisterViewModel { UserName = "duplicate", Email = "duplicate@agora.local", DisplayName = "Second", Password = "TestPassword!1", ConfirmPassword = "TestPassword!1" });

        Assert.IsType<ViewResult>(result);
        Assert.False(second.ModelState.IsValid);
        Assert.Equal(1, await fixture.Context.Users.CountAsync());
    }

    [Fact]
    public async Task Login_accepts_valid_credentials_and_rejects_invalid_ones()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("login.user", "login@agora.local");
        var valid = fixture.CreateAccountController();
        var validResult = await valid.Login(new LoginViewModel { UserNameOrEmail = user.Email!, Password = "TestPassword!1", RememberMe = true });
        var invalid = fixture.CreateAccountController();
        var invalidResult = await invalid.Login(new LoginViewModel { UserNameOrEmail = user.UserName!, Password = "wrong-password" });

        Assert.IsType<RedirectToActionResult>(validResult);
        Assert.True(fixture.SignInManager.SignInCalled);
        Assert.IsType<ViewResult>(invalidResult);
        Assert.False(invalid.ModelState.IsValid);
    }

    [Fact]
    public async Task Logout_is_post_antiforgery_and_signs_out()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var controller = fixture.CreateAccountController();
        var result = await controller.Logout();

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(fixture.SignInManager.SignOutCalled);
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Logout))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), false).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false).SingleOrDefault());
    }

    [Fact]
    public async Task Profile_update_only_changes_authenticated_user_and_validates_lengths()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var actor = await fixture.CreateUserAsync("actor", "actor@agora.local");
        var other = await fixture.CreateUserAsync("other", "other@agora.local");
        var controller = fixture.CreateAccountController(actor.Id);
        var result = await controller.Profile(new ProfileViewModel { DisplayName = "Updated Actor", Bio = "Updated bio" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Updated Actor", (await fixture.UserManager.FindByIdAsync(actor.Id))!.DisplayName);
        Assert.Equal("Other", (await fixture.UserManager.FindByIdAsync(other.Id))!.DisplayName);
        var invalid = new ProfileViewModel { DisplayName = "Name", Bio = new string('x', 501) };
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));
    }

    [Fact]
    public void Moderation_is_server_authorized_for_moderator_and_admin_only()
    {
        var attribute = typeof(Areas.Moderation.Controllers.HomeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();

        Assert.Equal("Moderator,Admin", attribute.Roles);
        Assert.DoesNotContain("Member", attribute.Roles);
    }

    private sealed class IdentityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private IdentityFixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
            _scope = provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<AgoraDbContext>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            SignInManager = new TestSignInManager(
                UserManager,
                _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
                _scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                _scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>(),
                _scope.ServiceProvider.GetRequiredService<ILogger<SignInManager<ApplicationUser>>>(),
                _scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>(),
                _scope.ServiceProvider.GetRequiredService<IUserConfirmation<ApplicationUser>>());
        }

        public AgoraDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole> RoleManager { get; }
        public TestSignInManager SignInManager { get; }

        public static async Task<IdentityFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddIdentity<ApplicationUser, IdentityRole>(options => options.User.RequireUniqueEmail = true)
                .AddEntityFrameworkStores<AgoraDbContext>();
            var provider = services.BuildServiceProvider();
            var fixture = new IdentityFixture(connection, provider);
            await fixture.Context.Database.EnsureCreatedAsync();
            Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole("Member"))).Succeeded);
            Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole("Moderator"))).Succeeded);
            Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            return fixture;
        }

        public AccountController CreateAccountController(string? userId = null)
        {
            var controller = new AccountController(UserManager, SignInManager, RoleManager)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
            if (userId is not null)
            {
                controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
            }
            return controller;
        }

        public async Task<ApplicationUser> CreateUserAsync(string userName, string email)
        {
            var user = new ApplicationUser { UserName = userName, Email = email, DisplayName = userName == "other" ? "Other" : "Actor" };
            Assert.True((await UserManager.CreateAsync(user, "TestPassword!1")).Succeeded);
            Assert.True((await UserManager.AddToRoleAsync(user, "Member")).Succeeded);
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _scope.Dispose();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestSignInManager : SignInManager<ApplicationUser>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public TestSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> options,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
        {
            _userManager = userManager;
        }
        public bool SignInCalled { get; private set; }
        public bool SignOutCalled { get; private set; }

        public override Task SignInAsync(ApplicationUser user, bool isPersistent, string? authenticationMethod = null)
        {
            SignInCalled = true;
            return Task.CompletedTask;
        }

        public override async Task<Microsoft.AspNetCore.Identity.SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure)
        {
            if (!await _userManager.CheckPasswordAsync(user, password)) return Microsoft.AspNetCore.Identity.SignInResult.Failed;
            SignInCalled = true;
            return Microsoft.AspNetCore.Identity.SignInResult.Success;
        }

        public override Task SignOutAsync()
        {
            SignOutCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
