using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Data.Seeding;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Argus.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSignalR();
var connectionString = builder.Configuration.GetConnectionString("AgoraDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:AgoraDb' is required. Configure it in User Secrets for local development.");

builder.Services.AddDbContext<AgoraDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddHealthChecks()
    .AddCheck<AgoraDatabaseHealthCheck>("AgoraDb");
builder.Services.AddArgus(builder.Configuration.GetSection("Argus"))
    .AddGauge<AgoraDbContext>("reports.pending", async db => (decimal)await db.Reports.CountAsync(report => report.Status == ReportStatus.Open))
    .OnMaintenanceModeChanged<AgoraArgusMessageHandler>((handler, message) => handler.HandleMaintenanceModeChangedAsync(message))
    .OnHealthStatusChanged<AgoraArgusMessageHandler>((handler, message) => handler.HandleHealthStatusChangedAsync(message))
    .OnInsightPublished<AgoraArgusMessageHandler>((handler, message) => handler.HandleInsightPublishedAsync(message));
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AgoraDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = true;
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true) return;
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.Principal);
        if (user?.IsBanned != true) return;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddScoped<IForumRepository, ForumRepository>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ILikesRepository, LikesRepository>();
builder.Services.AddScoped<ILikesService, LikesService>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IAiReportCheckService, AiReportCheckService>();
builder.Services.AddScoped<IUserManagementRepository, UserManagementRepository>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IBanStatusService, BanStatusService>();
builder.Services.AddScoped<IRuleRepository, RuleRepository>();
builder.Services.AddScoped<IRuleService, RuleService>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();
builder.Services.AddScoped<IMaintenanceStateService, MaintenanceStateService>();
builder.Services.AddScoped<IArgusPanelService, ArgusPanelService>();
builder.Services.AddScoped<AgoraArgusMessageHandler>();
builder.Services.AddSingleton<IPublicMaintenanceNotifier, PublicMaintenanceNotifier>();
builder.Services.AddScoped<DevelopmentDatabaseSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<AgoraDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<DevelopmentDatabaseSeeder>().SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Keep 404s friendly without re-executing maintenance 503 responses or API/SignalR endpoints.
app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    var request = statusContext.HttpContext.Request;
    if (response.StatusCode != StatusCodes.Status404NotFound || response.HasStarted ||
        request.Path.StartsWithSegments("/hubs") || request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        return;
    response.ContentType = "text/html; charset=utf-8";
    await response.WriteAsync("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Page not found · Agora</title><link rel=\"stylesheet\" href=\"/lib/bootstrap/dist/css/bootstrap.min.css\"></head><body><main class=\"container py-5\"><div class=\"card shadow-sm mx-auto\" style=\"max-width:42rem\"><div class=\"card-body p-4\"><h1 class=\"h2\">Page not found</h1><p class=\"text-muted\">The page you requested does not exist or is no longer available.</p><a class=\"btn btn-primary\" href=\"/\">Go to Home</a> <a class=\"btn btn-outline-secondary\" href=\"/Forums\">Browse forums</a></div></div></main></body></html>");
});

app.UseArgusMetrics();
app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<Agora.Web.Middleware.MaintenanceModeMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<Agora.Web.Hubs.AgoraNotificationHub>("/hubs/notifications");
app.MapHub<Agora.Web.Hubs.MaintenanceStatusHub>("/hubs/maintenance");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
