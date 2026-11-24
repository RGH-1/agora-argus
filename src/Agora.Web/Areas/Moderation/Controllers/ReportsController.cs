using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public sealed class ReportsController(IReportService reportService) : Controller
{
    public async Task<IActionResult> Index() => View(await reportService.GetOpenAsync());
}
