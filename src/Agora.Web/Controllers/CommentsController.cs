using Agora.Web.Data.Entities;
using Agora.Web.Models.Comments;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

public sealed class CommentsController(ICommentService commentService, IPostService postService, UserManager<ApplicationUser> userManager) : Controller
{
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCommentViewModel model)
    {
        if (!ModelState.IsValid) return await DetailsWithValidationAsync(model);
        var authorId = userManager.GetUserId(User);
        if (authorId is null) return Challenge();
        try
        {
            await commentService.CreateAsync(model.PostId, model.Content, authorId);
            TempData["StatusMessage"] = "Comment added.";
            return RedirectToAction("Details", "Posts", new { id = model.PostId });
        }
        catch (CommentNotFoundOrForbiddenException) { return NotFound(); }
        catch (CommentBusinessRuleException exception)
        {
            ModelState.AddModelError(nameof(model.Content), exception.Message);
            return await DetailsWithValidationAsync(model);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var requestorId = userManager.GetUserId(User);
        if (requestorId is null) return Challenge();
        try
        {
            var postId = await commentService.DeleteAsync(id, requestorId, User.IsInRole("Moderator") || User.IsInRole("Admin"));
            TempData["StatusMessage"] = "Comment deleted.";
            return RedirectToAction("Details", "Posts", new { id = postId });
        }
        catch (CommentNotFoundOrForbiddenException) { return NotFound(); }
    }

    private async Task<IActionResult> DetailsWithValidationAsync(CreateCommentViewModel model)
    {
        var post = await postService.GetPublicByIdAsync(model.PostId);
        if (post is null) return NotFound();
        var comments = await commentService.GetForPublicPostAsync(model.PostId);
        var userId = userManager.GetUserId(User);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        return View("~/Views/Posts/Details.cshtml", new Agora.Web.Models.Posts.PostDetailsViewModel
        {
            Post = post,
            Comments = comments,
            NewComment = model,
            IsCurrentUserBanned = user?.IsBanned == true
        });
    }
}
