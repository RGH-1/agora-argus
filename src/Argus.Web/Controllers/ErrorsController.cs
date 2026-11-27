using Argus.Web.Models.Errors;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class ErrorsController(IClientAppService clientAppService, IHealthService healthService) : Controller
{
    /// <summary>The newest errors only: enough to see what went wrong during an incident.</summary>
    private const int Limit = 100;

    /// <summary>The error log (§4.2) of one client; the first client when none is chosen.</summary>
    public async Task<IActionResult> Index(string? clientId, CancellationToken cancellationToken)
    {
        var clients = await clientAppService.GetAllAsync(cancellationToken);
        var client = clientId is null ? clients.FirstOrDefault() : clients.FirstOrDefault(app => app.ClientId == clientId);
        if (client is null) return NotFound();

        return View(new ErrorLogViewModel
        {
            Clients = clients,
            Client = client,
            Errors = await healthService.GetLatestErrorsAsync(client.Id, Limit, cancellationToken),
            Limit = Limit,
        });
    }
}