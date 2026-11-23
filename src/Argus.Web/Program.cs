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
