using Agora.Web.Models.Moderation;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public class ForumsController(IForumService forumService) : Controller
{
    public async Task<IActionResult> Index() => View(await forumService.GetPendingAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        try { await forumService.ApproveAsync(id); TempData["StatusMessage"] = "Forum approved and published."; }
        catch (ForumModerationException exception) { TempData["ErrorMessage"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Reject(int id)
    {
        var forum = await forumService.GetPendingByIdAsync(id);
        return forum is null ? NotFound() : View(new RejectForumViewModel { ForumId = forum.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectForumViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await forumService.RejectAsync(model.ForumId, model.Reason); TempData["StatusMessage"] = "Forum rejected; the owner can see the reason."; return RedirectToAction(nameof(Index)); }
        catch (ForumBusinessRuleException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
        catch (ForumModerationException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
    }
}
