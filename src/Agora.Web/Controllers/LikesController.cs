using Agora.Web.Data.Entities;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

[Authorize]
public sealed class LikesController(ILikesService likesService, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        try
        {
            await likesService.ToggleAsync(postId, userId);
            return RedirectToAction("Details", "Posts", new { id = postId });
        }
        catch (LikeNotFoundOrForbiddenException) { return NotFound(); }
    }
}
