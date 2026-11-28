using Argus.Contracts;
using Argus.Web.Consumers;
using Argus.Web.Data;
using Argus.Web.Hubs;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Argus.Web.Data.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Every page needs a logged-in user unless it says [AllowAnonymous] (only login, access denied and the error page).
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AuthorizeFilter()));

var connectionString = builder.Configuration.GetConnectionString("ArgusDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:ArgusDb' is required. Configure it in User Secrets for local development.");
builder.Services.AddDbContext<ArgusDbContext>(options => options.UseSqlServer(connectionString));


// Identity (§4): Argus staff only. No public registration; the Admin creates operator accounts.
builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ArgusDbContext>();
// Checks the account on every request, so a deleted operator is signed out at once instead of when the cookie expires.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<MetricSeeder>();

builder.Services.AddScoped<IOperatorService, OperatorService>();
builder.Services.AddScoped<IClientAppRepository, ClientAppRepository>();
builder.Services.AddScoped<IClientAppService, ClientAppService>();
builder.Services.AddScoped<IHealthRepository, HealthRepository>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IMetricRepository, MetricRepository>();
builder.Services.AddScoped<IMetricService, MetricService>();

// RabbitMQ: the same shared connection the SDK uses. Argus declares only its own queues; each client declares its inbox.
var rabbitMq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
if (string.IsNullOrEmpty(rabbitMq.Password))
    throw new InvalidOperationException("RabbitMq:Password is missing. Configure it in User Secrets for local development.");
builder.Services.AddSingleton(provider => new ArgusConnection(rabbitMq, "argus",
        (channel, ct) => ArgusTopology.DeclareIngestAsync(channel, ct),
        provider.GetRequiredService<ILogger<ArgusConnection>>()
    ));
builder.Services.AddHostedService(provider => provider.GetRequiredService<ArgusConnection>());
builder.Services.AddSingleton<ArgusPublisher>();
builder.Services.AddSingleton<IClientNotifier, ClientNotifier>();

// Live status board and incident alerts (§4.2): Argus pushes changes to the open pages.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IStatusBoardNotifier, StatusBoardNotifier>();

builder.Services.AddHostedService<HealthConsumer>();
builder.Services.AddHostedService<AnalyticsConsumer>();
builder.Services.AddHostedService<HealthWatchdog>();

var app = builder.Build();

// Create or update ArgusDb on every start (design draft §8). This also seeds the agora and demo client apps,
// then the Admin and Operator accounts and the default charts.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ArgusDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<MetricSeeder>().SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<StatusHub>("/hubs/status");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
