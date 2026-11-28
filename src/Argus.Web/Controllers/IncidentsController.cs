using System.ComponentModel.DataAnnotations;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class IncidentsController(IClientAppService clientAppService, IIncidentService incidentService) : Controller
{
    /// <summary>The incidents of one client: the one chosen in the client switcher, or ?clientId= (from the status board).</summary>
    public async Task<IActionResult> Index(string? clientId, CancellationToken cancellationToken)
    {
        var client = ClientSelection.Resolve(HttpContext, await clientAppService.GetAllAsync(cancellationToken), clientId);
        if (client is null) return NotFound();

        ViewData["Client"] = client;
        return View(await incidentService.GetAllAsync(client.ClientId, cancellationToken));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        await incidentService.GetByIdAsync(id, cancellationToken) is { } incident ? View(incident) : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken cancellationToken) =>
        await incidentService.AcknowledgeAsync(id, cancellationToken) ? RedirectToAction(nameof(Details), new { id }) : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, CancellationToken cancellationToken) =>
        await incidentService.ResolveAsync(id, cancellationToken) ? RedirectToAction(nameof(Details), new { id }) : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Notes(int id, [StringLength(2000)] string? notes, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return await incidentService.SaveNotesAsync(id, notes, cancellationToken) ? RedirectToAction(nameof(Details), new { id }) : NotFound();
    }
}