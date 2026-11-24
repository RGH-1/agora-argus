using Agora.Web.Models.Rules;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Areas.Moderation.Controllers;

[Area("Moderation")]
[Authorize(Roles = "Moderator,Admin")]
public sealed class RulesController(IRuleService ruleService) : Controller
{
    public async Task<IActionResult> Index() => View(await ruleService.GetOrderedAsync());
    [HttpGet] public IActionResult Create() => View(new RuleFormViewModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RuleFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await ruleService.CreateAsync(model.Title, model.Content); TempData["StatusMessage"] = "Rule created."; return RedirectToAction(nameof(Index)); }
        catch (RuleBusinessRuleException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await ruleService.GetByIdAsync(id);
        return rule is null ? NotFound() : View(new RuleFormViewModel { Id = rule.Id, Title = rule.Title, Content = rule.Content });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RuleFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await ruleService.UpdateAsync(model.Id, model.Title, model.Content); TempData["StatusMessage"] = "Rule updated."; return RedirectToAction(nameof(Index)); }
        catch (RuleBusinessRuleException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(model); }
        catch (RuleNotFoundException) { return NotFound(); }
    }
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await ruleService.GetByIdAsync(id);
        return rule is null ? NotFound() : View(rule);
    }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try { await ruleService.DeleteAsync(id); TempData["StatusMessage"] = "Rule deleted."; }
        catch (RuleNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUp(int id) => await MoveAsync(id, -1);
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDown(int id) => await MoveAsync(id, 1);
    private async Task<IActionResult> MoveAsync(int id, int direction)
    {
        var result = await ruleService.MoveAsync(id, direction);
        TempData["StatusMessage"] = result == RuleMoveResult.Moved ? "Rule order updated." : null;
        TempData["ErrorMessage"] = result == RuleMoveResult.Boundary ? "That rule cannot be moved further." : result == RuleMoveResult.NotFound ? "The rule no longer exists." : null;
        return RedirectToAction(nameof(Index));
    }
}
