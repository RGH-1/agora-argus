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
builder.Services.AddControllersWithViews();
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
