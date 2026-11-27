using Argus.Web.Models.Dashboard;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class DashboardController(IClientAppService clientAppService, IMetricService metricService) : Controller
{
    /// <summary>The time range selector's options; the dashboard shows the range up to now.</summary>
    public static readonly IReadOnlyDictionary<string, TimeSpan> Ranges = new Dictionary<string, TimeSpan>
    {
        ["hour"] = TimeSpan.FromHours(1),
        ["day"] = TimeSpan.FromDays(1),
        ["week"] = TimeSpan.FromDays(7),
    };

    /// <summary>The charts of one client (§4.3); the first client when none is chosen.</summary>
    public async Task<IActionResult> Index(string? clientId, string range = "day", CancellationToken cancellationToken = default)
    {
        if (!Ranges.TryGetValue(range, out var length)) return BadRequest();

        var clients = await clientAppService.GetAllAsync(cancellationToken);
        var client = clientId is null ? clients.FirstOrDefault() : clients.FirstOrDefault(app => app.ClientId == clientId);
        if (client is null) return NotFound();

        var toUtc = DateTime.UtcNow;
        return View(new DashboardViewModel
        {
            Clients = clients,
            Client = client,
            Range = range,
            Charts = await metricService.GetDashboardAsync(client.Id, toUtc - length, toUtc, cancellationToken),
        });
    }
}