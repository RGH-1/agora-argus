using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public sealed class ArgusController(IArgusPanelService panel) : Controller
{
    public async Task<IActionResult> Index() => View(await panel.GetAsync(HttpContext.RequestAborted));
}
