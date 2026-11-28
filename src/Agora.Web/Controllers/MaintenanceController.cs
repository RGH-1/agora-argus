using Agora.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MaintenanceController(IMaintenanceStateService maintenance) : Controller
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status() => Json(await maintenance.GetAsync(HttpContext.RequestAborted));
}
