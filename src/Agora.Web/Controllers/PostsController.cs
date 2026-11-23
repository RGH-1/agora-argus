using Agora.Web.Data.Entities;
using Agora.Web.Models.Posts;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

public class PostsController(IPostService postService, ICommentService commentService, UserManager<ApplicationUser> userManager) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var post = await postService.GetPublicByIdAsync(id);
        if (post is null) return NotFound();
        var comments = await commentService.GetForPublicPostAsync(id);
        var userId = userManager.GetUserId(User);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        return View(new PostDetailsViewModel
        {
            Post = post,
            Comments = comments,
            NewComment = new(),
            IsCurrentUserBanned = user?.IsBanned == true
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create(int forumId)
    {
        try { await postService.EnsureForumApprovedAsync(forumId); return View(new CreatePostViewModel { ForumId = forumId }); }
        catch (PostNotFoundOrForbiddenException) { return NotFound(); }
        catch (PostBusinessRuleException) { return NotFound(); }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var authorId = userManager.GetUserId(User);
        if (authorId is null) return Challenge();
        try
        {
            var post = await postService.CreateAsync(model.ForumId, model.Title, model.Content, authorId);
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }
        catch (PostNotFoundOrForbiddenException) { return NotFound(); }
        catch (PostBusinessRuleException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await GetOwnedPostAsync(id);
        return post is null ? NotFound() : View(new EditPostViewModel { Id = post.Id, Title = post.Title, Content = post.Content });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditPostViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var authorId = userManager.GetUserId(User);
        if (authorId is null) return Challenge();
        try { await postService.UpdateAsync(model.Id, model.Title, model.Content, authorId); return RedirectToAction(nameof(Details), new { id = model.Id }); }
        catch (PostNotFoundOrForbiddenException) { return NotFound(); }
        catch (PostBusinessRuleException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await GetOwnedPostAsync(id);
        return post is null ? NotFound() : View(post);
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var authorId = userManager.GetUserId(User);
        if (authorId is null) return Challenge();
        try { await postService.RemoveAsync(id, authorId); return RedirectToAction("Index", "Forums"); }
        catch (PostNotFoundOrForbiddenException) { return NotFound(); }
    }

    private async Task<Post?> GetOwnedPostAsync(int id)
    {
        var authorId = userManager.GetUserId(User);
        return authorId is null ? null : await postService.GetOwnedActiveAsync(id, authorId);
    }
}
