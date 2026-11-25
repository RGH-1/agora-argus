using Argus.Contracts;
using Argus.Web.Consumers;
using Argus.Web.Data;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("ArgusDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:ArgusDb' is required. Configure it in User Secrets for local development.");
builder.Services.AddDbContext<ArgusDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddScoped<IClientAppRepository, ClientAppRepository>();
builder.Services.AddScoped<IClientAppService, ClientAppService>();
builder.Services.AddScoped<IHealthRepository, HealthRepository>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IIncidentService, IncidentService>();

// RabbitMQ: the same shared connection the SDK uses. Argus declares only its own queues; each client declares its inbox.
var rabbitMq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
if (string.IsNullOrEmpty(rabbitMq.Password))
    throw new InvalidOperationException("RabbitMq:Password is missing. Configure it in User Secrets for local development.");
builder.Services.AddSingleton(provider => new ArgusConnection(rabbitMq, "argus",
        (channel, ct) => ArgusTopology.DeclareIngestAsync(channel, ct),
        provider.GetRequiredService<ILogger<ArgusConnection>>()
    ));
builder.Services.AddHostedService(provider => provider.GetRequiredService<ArgusConnection>());
builder.Services.AddHostedService<HealthConsumer>();
builder.Services.AddHostedService<AnalyticsConsumer>();
builder.Services.AddHostedService<HealthWatchdog>();

var app = builder.Build();

// Create or update ArgusDb on every start (design draft §8). This also seeds the agora and demo client apps.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ArgusDbContext>().Database.MigrateAsync();
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
