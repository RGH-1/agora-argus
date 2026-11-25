using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public sealed class UsersController(IUserManagementService service, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1) => View(await service.GetPageAsync(search, page));
    [HttpGet] public async Task<IActionResult> Ban(string id) => await FormAsync(id, true);
    [HttpGet] public async Task<IActionResult> Unban(string id) => await FormAsync(id, false);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Ban(UserBanFormViewModel model) => ChangeBanAsync(model, true);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Unban(UserBanFormViewModel model) => ChangeBanAsync(model, false);
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> GrantModerator(string userId) => ChangeRoleAsync(userId, true);
    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> RemoveModerator(string userId) => ChangeRoleAsync(userId, false);
    private async Task<IActionResult> FormAsync(string id, bool ban) { var model = await service.GetBanFormAsync(id); return model is null ? NotFound() : View(ban ? nameof(Ban) : nameof(Unban), model); }
    private async Task<IActionResult> ChangeBanAsync(UserBanFormViewModel model, bool ban)
    {
        if (!ModelState.IsValid)
        {
            var details = await service.GetBanFormAsync(model.UserId);
            if (details is null) return NotFound();
            details.Reason = model.Reason;
            return View(ban ? nameof(Ban) : nameof(Unban), details);
        }
        var actorId = users.GetUserId(User); if (actorId is null) return Challenge();
        try { await service.ChangeBanAsync(model.UserId, actorId, User.IsInRole("Admin"), ban, model.Reason); TempData["StatusMessage"] = ban ? "User banned." : "User unbanned."; }
        catch (UserManagementException exception) { TempData["ErrorMessage"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }
    private async Task<IActionResult> ChangeRoleAsync(string id, bool grant)
    {
        var actorId = users.GetUserId(User); if (actorId is null) return Challenge();
        try { await service.ChangeModeratorRoleAsync(id, actorId, grant); TempData["StatusMessage"] = grant ? "Moderator role granted." : "Moderator role removed."; }
        catch (UserManagementException exception) { TempData["ErrorMessage"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }
}
