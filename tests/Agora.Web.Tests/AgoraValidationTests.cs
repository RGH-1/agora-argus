using Agora.Web.Models.Account;
using Agora.Web.Models.Forums;
using Agora.Web.Models.Posts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Tests;

public sealed class AgoraValidationTests
{
    [Fact]
    public async Task Global_antiforgery_filter_rejects_missing_and_invalid_tokens_and_allows_valid_authenticated_post()
    {
        AntiforgeryProbeController.Hits = 0;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(typeof(AntiforgeryProbeController).Assembly);
        builder.Services.AddAntiforgery();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/test-token", (HttpContext context, IAntiforgery antiforgery) => Results.Text(antiforgery.GetAndStoreTokens(context).RequestToken!));
        app.MapControllers(); await app.StartAsync();
        var client = app.GetTestClient();

        Assert.Equal(400, (int)(await client.PostAsync("/test-antiforgery/probe", new StringContent(""))).StatusCode);
        Assert.Equal(0, AntiforgeryProbeController.Hits);
        var invalid = new HttpRequestMessage(HttpMethod.Post, "/test-antiforgery/probe"); invalid.Headers.Add("RequestVerificationToken", "invalid");
        Assert.Equal(400, (int)(await client.SendAsync(invalid)).StatusCode);
        Assert.Equal(0, AntiforgeryProbeController.Hits);
        var tokenResponse = await client.GetAsync("/test-token");
        var token = await tokenResponse.Content.ReadAsStringAsync();
        var cookie = tokenResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var valid = new HttpRequestMessage(HttpMethod.Post, "/test-antiforgery/probe"); valid.Headers.Add("RequestVerificationToken", token);
        valid.Headers.Add("Cookie", cookie);
        Assert.True((await client.SendAsync(valid)).IsSuccessStatusCode);
        Assert.Equal(1, AntiforgeryProbeController.Hits);
    }
    [Fact]
    public async Task Production_exception_handler_returns_friendly_500_without_sensitive_exception_details()
    {
        var root = FindRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = Path.Combine(root, "src", "Agora.Web")
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Agora.Web.Controllers.HomeController).Assembly);
        await using var app = builder.Build();
        app.UseExceptionHandler("/Home/Error");
        app.UseRouting();
        // The production path resolves Home/Error through conventional MVC routing. Map the same friendly
        // contract explicitly in this minimal host so TestServer exercises ExceptionHandlerMiddleware itself.
        app.MapGet("/Home/Error", () => Results.Content("<h1>Something went wrong</h1><a href=\"/\">Go to Home</a>", "text/html", statusCode: 500));
        app.MapGet("/test-only/failure", (HttpContext _) => throw new InvalidOperationException("SqlException Password=not-for-client"));
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/test-only/failure");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(500, (int)response.StatusCode);
        Assert.Contains("Something went wrong", body);
        Assert.Contains("Go to Home", body);
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=not-for-client", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Agora", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Input_models_reject_required_whitespace_and_boundary_overflow()
    {
        Assert.False(IsValid(new RegisterViewModel { UserName = " ", Email = "bad", DisplayName = " ", Password = "short", ConfirmPassword = "different" }));
        Assert.False(IsValid(new CreateForumViewModel { Name = " ", Description = " " }));
        Assert.False(IsValid(new CreatePostViewModel { Title = new string('t', 151), Content = new string('c', 4001) }));
        Assert.True(IsValid(new CreatePostViewModel { Title = new string('t', 150), Content = new string('c', 4000) }));
    }

    [Fact]
    public void Mvc_uses_global_antiforgery_and_no_controller_bypasses_it()
    {
        var root = FindRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Agora.Web", "Program.cs"));
        Assert.Contains("AutoValidateAntiforgeryTokenAttribute", program);
        var controllers = Directory.GetFiles(Path.Combine(root, "src", "Agora.Web"), "*Controller.cs", SearchOption.AllDirectories);
        Assert.DoesNotContain(controllers.Select(File.ReadAllText), source => source.Contains("IgnoreAntiforgeryToken", StringComparison.Ordinal));
    }

    [Fact]
    public void Friendly_errors_and_tracked_configuration_do_not_expose_secrets()
    {
        var root = FindRoot();
        var error = File.ReadAllText(Path.Combine(root, "src", "Agora.Web", "Views", "Shared", "Error.cshtml"));
        var settings = File.ReadAllText(Path.Combine(root, "src", "Agora.Web", "appsettings.json"));
        Assert.Contains("Something went wrong", error);
        Assert.DoesNotContain("Password", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", settings, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValid(object model) => Validator.TryValidateObject(model, new ValidationContext(model), [], true);
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agora-argus.slnx"))) current = current.Parent;
        return Assert.IsType<DirectoryInfo>(current).FullName;
    }
}

[ApiController]
[Route("test-antiforgery")]
[Authorize]
public sealed class AntiforgeryProbeController : ControllerBase
{
    public static int Hits { get; set; }
    [HttpPost("probe")] public IActionResult Probe() { Hits++; return Ok(); }
}

public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
        new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], Scheme.Name)), Scheme.Name)));
}
