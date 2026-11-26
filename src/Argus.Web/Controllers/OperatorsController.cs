using Argus.Web.Data;
using Argus.Web.Models.Operators;
using Argus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

/// <summary>Admin only (§4): an Operator who opens this page gets "access denied".</summary>
[Authorize(Roles = ArgusRoles.Admin)]
public class OperatorsController(IOperatorService operatorService) : Controller
{
    public async Task<IActionResult> Index() =>
        View(new OperatorsViewModel { Accounts = await operatorService.GetAllAsync() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(OperatorsViewModel.NewOperator))] NewOperatorViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await operatorService.CreateOperatorAsync(model.UserName, model.Password);
            if (result.Succeeded)
            {
                TempData["Message"] = $"Operator '{model.UserName.Trim()}' created.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }

        model.Password = string.Empty; // never send a password back to the browser
        return View(nameof(Index), new OperatorsViewModel { Accounts = await operatorService.GetAllAsync(), NewOperator = model });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await operatorService.DeleteOperatorAsync(id)) return NotFound();
        TempData["Message"] = "Operator deleted.";
        return RedirectToAction(nameof(Index));
    }
}