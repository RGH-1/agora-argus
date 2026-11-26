using System.ComponentModel.DataAnnotations;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class ClientAppsController(IClientAppService clientAppService) : Controller
{
    /// <summary>The maintenance button on the status board: switches it on or off and tells the client.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Maintenance(string clientId, bool enabled, [StringLength(200)] string? message, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return await clientAppService.SetMaintenanceAsync(clientId, enabled, message, cancellationToken)
            ? RedirectToAction("Index", "Home")
            : NotFound();
    }
}