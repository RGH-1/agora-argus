using System.ComponentModel.DataAnnotations;
using Argus.Web.Data;
using Argus.Web.Data.Seeding;
using Argus.Web.Models.ClientApps;
using Argus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

/// <summary>
/// Managing client apps is Admin only (§4); maintenance and the client switcher are for every logged-in user,
/// so the role is on each Admin action instead of on the class.
/// </summary>
public class ClientAppsController(IClientAppService clientAppService, MetricSeeder metricSeeder) : Controller
{
    [Authorize(Roles = ArgusRoles.Admin)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await clientAppService.GetAllAsync(cancellationToken));

    [Authorize(Roles = ArgusRoles.Admin)]
    public IActionResult Create() => View(new NewClientAppViewModel());

    [Authorize(Roles = ArgusRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewClientAppViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await clientAppService.CreateAsync(model.ClientId, model.Name, cancellationToken);
        }
        catch (InvalidOperationException ex) // the ClientId is taken
        {
            ModelState.AddModelError(nameof(model.ClientId), ex.Message);
            return View(model);
        }

        await metricSeeder.SeedAsync(); // the same seeder as at startup: the new client gets its sys.* charts right away
        TempData["Message"] = $"Client app '{model.ClientId}' created. Set its health thresholds below.";
        return RedirectToAction(nameof(Edit), new { id = model.ClientId });
    }

    [Authorize(Roles = ArgusRoles.Admin)]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var client = await clientAppService.GetByClientIdAsync(id, cancellationToken);
        if (client is null) return NotFound();

        ViewData["ClientId"] = client.ClientId;
        return View(new ClientAppSettings
        {
            Name = client.Name,
            IsSuspended = client.IsSuspended,
            HeartbeatTimeoutSec = client.HeartbeatTimeoutSec,
            SlowDbMs = client.SlowDbMs,
            MaxErrorRatePercent = client.MaxErrorRatePercent,
        });
    }

    [Authorize(Roles = ArgusRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ClientAppSettings settings, CancellationToken cancellationToken)
    {
        ViewData["ClientId"] = id;
        if (!ModelState.IsValid) return View(settings);
        if (!await clientAppService.UpdateAsync(id, settings, cancellationToken)) return NotFound();

        TempData["Message"] = $"Client app '{id}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = ArgusRoles.Admin)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        await clientAppService.GetByClientIdAsync(id, cancellationToken) is { } client ? View(client) : NotFound();

    [Authorize(Roles = ArgusRoles.Admin)]
    [HttpPost, ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id, CancellationToken cancellationToken)
    {
        if (!await clientAppService.DeleteAsync(id, cancellationToken)) return NotFound();
        TempData["Message"] = $"Client app '{id}' and all its data deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The client switcher in the layout: remembers the choice and goes back to the same page.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(string clientId, string? returnUrl, CancellationToken cancellationToken)
    {
        if (await clientAppService.GetByClientIdAsync(clientId, cancellationToken) is null) return NotFound();
        ClientSelection.Remember(HttpContext, clientId);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/"); // only local addresses, like the login page
    }

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