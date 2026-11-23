using Agora.Web.Models.Forums;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Agora.Web.Data.Entities;

namespace Agora.Web.Controllers;

public class ForumsController(IForumService forumService, IPostService postService, ILikesService likesService, UserManager<ApplicationUser> userManager) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index() => View(await forumService.GetPublicAsync());

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var forum = await forumService.GetPublicByIdAsync(id);
        if (forum is null) return NotFound();
        var posts = await postService.GetPublicByForumAsync(id);
        return View(new ForumDetailsViewModel { Forum = forum, Posts = posts, LikeCounts = await likesService.GetCountsForPostsAsync(posts.Select(post => post.Id)) });
    }

    [Authorize]
    public async Task<IActionResult> Mine()
    {
        var userId = userManager.GetUserId(User);
        return userId is null ? Challenge() : View(await forumService.GetMineAsync(userId));
    }

    [Authorize]
    [HttpGet]
    public IActionResult Create() => View(new CreateForumViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateForumViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        try
        {
            await forumService.CreateAsync(model.Name, model.Description, userId);
            TempData["StatusMessage"] = "Your forum was submitted for moderation.";
            return RedirectToAction(nameof(Mine));
        }
        catch (ForumBusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var forum = await GetOwnedForumAsync(id);
        if (forum is null) return NotFound();
        ViewData["ForumStatus"] = forum.Status;
        return View(new EditForumViewModel { Id = forum.Id, Description = forum.Description! });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditForumViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        try
        {
            var previous = await forumService.GetOwnedByIdAsync(model.Id, userId);
            if (previous is null) return NotFound();
            var result = await forumService.UpdateDescriptionAsync(model.Id, model.Description, userId);
            TempData["StatusMessage"] = result == ForumDescriptionUpdateResult.Unchanged
                ? "No changes were made to your forum."
                : previous.Status == ForumStatus.Approved || previous.Status == ForumStatus.Rejected
                    ? "Your forum was resubmitted for approval."
                    : "Forum description updated while it remains under review.";
            return RedirectToAction(nameof(Mine));
        }
        catch (ForumNotFoundOrForbiddenException)
        {
            return NotFound();
        }
        catch (ForumBusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var forum = await GetOwnedForumAsync(id);
        return forum is null ? NotFound() : View(forum);
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        try
        {
            await forumService.DeleteAsync(id, userId);
            TempData["StatusMessage"] = "Forum deleted.";
            return RedirectToAction(nameof(Mine));
        }
        catch (ForumNotFoundOrForbiddenException)
        {
            return NotFound();
        }
    }

    private async Task<Forum?> GetOwnedForumAsync(int id)
    {
        var userId = userManager.GetUserId(User);
        return userId is null ? null : await forumService.GetOwnedByIdAsync(id, userId);
    }
}
