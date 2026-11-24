using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public sealed class ReportsController(IReportService reportService, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index() => View(await reportService.GetOpenAsync());

    public async Task<IActionResult> History() => View(await reportService.GetHistoryAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(int reportId) => ResolveAsync(reportId, ModerationActionType.RemovePost, null, "Post removed and related open reports closed.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Dismiss(int reportId) => ResolveAsync(reportId, ModerationActionType.DismissReport, null, "Report dismissed.");

    [HttpGet]
    public IActionResult Warn(int reportId) => View(new ModerationReasonViewModel { ReportId = reportId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Warn(ModerationReasonViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        return await ResolveAsync(model.ReportId, ModerationActionType.WarnAuthor, model.Reason, "Warning issued and report reviewed.");
    }

    [HttpGet]
    public IActionResult Ban(int reportId) => View(new ModerationReasonViewModel { ReportId = reportId });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(ModerationReasonViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        return await ResolveAsync(model.ReportId, ModerationActionType.BanAuthor, model.Reason, "Author banned and report reviewed.");
    }

    private async Task<IActionResult> ResolveAsync(int reportId, ModerationActionType action, string? reason, string successMessage)
    {
        var moderatorId = userManager.GetUserId(User);
        if (moderatorId is null) return Challenge();
        try
        {
            await reportService.ResolveAsync(reportId, moderatorId, action, reason);
            TempData["StatusMessage"] = successMessage;
        }
        catch (ReportAlreadyHandledException exception) { TempData["ErrorMessage"] = exception.Message; }
        catch (ReportBusinessRuleException exception) { TempData["ErrorMessage"] = exception.Message; }
        catch (ReportNotFoundOrForbiddenException) { return NotFound(); }
        return RedirectToAction(nameof(Index));
    }
}
