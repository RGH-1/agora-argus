using Argus.Web.Models;
using Argus.Web.Models.Status;
using Argus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Argus.Web.Controllers;

public class HomeController(IClientAppService clientAppService, IHealthService healthService, IIncidentService incidentService) : Controller
{
    /// <summary>The status board (§4.2): one card per client app.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await GetBoardAsync(cancellationToken));

    /// <summary>Only the cards: the status board reloads them when Argus pushes a change (AA-40).</summary>
    public async Task<IActionResult> Board(CancellationToken cancellationToken) => PartialView("_Board", await GetBoardAsync(cancellationToken));

    private async Task<List<ClientStatusViewModel>> GetBoardAsync(CancellationToken cancellationToken)
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
        return board;
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous] // the error page must work for someone who isn't logged in too
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}