using Agora.Web.Data.Entities;
using Agora.Web.Models.Reports;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

[Authorize]
public sealed class ReportsController(IReportService reportService, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int postId)
    {
        var reporterId = userManager.GetUserId(User);
        if (reporterId is null) return Challenge();
        try
        {
            var post = await reportService.GetReportablePostAsync(postId, reporterId);
            ViewData["PostTitle"] = post.Title;
            return View(new CreateReportViewModel { PostId = postId });
        }
        catch (ReportNotFoundOrForbiddenException) { return NotFound(); }
        catch (ReportBusinessRuleException exception) { return BadRequest(exception.Message); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReportViewModel model)
    {
        var reporterId = userManager.GetUserId(User);
        if (reporterId is null) return Challenge();
        if (!ModelState.IsValid) return await CreateWithValidationAsync(model, reporterId);
        try
        {
            await reportService.CreateAsync(model.PostId, model.Category, model.Details, reporterId);
            TempData["StatusMessage"] = "Your report has been submitted successfully.";
            return RedirectToAction("Details", "Posts", new { id = model.PostId });
        }
        catch (ReportNotFoundOrForbiddenException) { return NotFound(); }
        catch (OpenReportAlreadyExistsException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await CreateWithValidationAsync(model, reporterId);
        }
        catch (ReportBusinessRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await CreateWithValidationAsync(model, reporterId);
        }
    }

    private async Task<IActionResult> CreateWithValidationAsync(CreateReportViewModel model, string reporterId)
    {
        try
        {
            var post = await reportService.GetReportablePostAsync(model.PostId, reporterId);
            ViewData["PostTitle"] = post.Title;
            return View(nameof(Create), model);
        }
        catch (ReportNotFoundOrForbiddenException) { return NotFound(); }
        catch (ReportBusinessRuleException exception) { return BadRequest(exception.Message); }
    }
}
