using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Data.Seeding;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
var connectionString = builder.Configuration.GetConnectionString("AgoraDb")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:AgoraDb' is required. Configure it in User Secrets for local development.");

builder.Services.AddDbContext<AgoraDbContext>(options =>
    options.UseSqlServer(connectionString));
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
});

builder.Services.AddScoped<IForumRepository, ForumRepository>();
builder.Services.AddScoped<IForumService, ForumService>();
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

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
