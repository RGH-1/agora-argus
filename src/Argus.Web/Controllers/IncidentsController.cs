using System.ComponentModel.DataAnnotations;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

public class IncidentsController(IIncidentService incidentService) : Controller
{
    /// <summary>All incidents, or one client's (the status board links here with ?clientId=agora).</summary>
    public async Task<IActionResult> Index(string? clientId, CancellationToken cancellationToken)
    {
        ViewData["ClientId"] = clientId;
        return View(await incidentService.GetAllAsync(clientId, cancellationToken));
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