using System.Diagnostics;
using Argus.Web.Models;
using Argus.Web.Models.Status;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class HomeController(IClientAppService clientAppService, IHealthService healthService, IIncidentService incidentService) : Controller
{
    /// <summary>The status board (§4.2): one card per client app.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var board = new List<ClientStatusViewModel>();
        foreach (var client in await clientAppService.GetAllAsync(cancellationToken)) // only a handful of clients, so one small query each is fine
        {
            board.Add(new ClientStatusViewModel
            {
                Client = client,
                Latest = await healthService.GetLatestHeartbeatAsync(client.Id, cancellationToken),
                OpenIncident = await incidentService.GetOpenAsync(client.Id, cancellationToken),
            });
        }
        return View(board);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}