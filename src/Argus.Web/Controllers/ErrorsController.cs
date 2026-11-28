using Argus.Web.Models.Errors;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class ErrorsController(IClientAppService clientAppService, IHealthService healthService) : Controller
{
    /// <summary>The newest errors only: enough to see what went wrong during an incident.</summary>
    private const int Limit = 100;

    /// <summary>The error log (§4.2) of one client: the one chosen in the client switcher, or ?clientId=.</summary>
    public async Task<IActionResult> Index(string? clientId, CancellationToken cancellationToken)
    {
        var clients = await clientAppService.GetAllAsync(cancellationToken);
        var client = ClientSelection.Resolve(HttpContext, clients, clientId);
        if (client is null) return NotFound();

        return View(new ErrorLogViewModel
        {
            Client = client,
            Errors = await healthService.GetLatestErrorsAsync(client.Id, Limit, cancellationToken),
            Limit = Limit,
        });
    }
}