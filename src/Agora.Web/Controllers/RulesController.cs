using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

[AllowAnonymous]
public sealed class RulesController(IRuleService ruleService) : Controller
{
    public async Task<IActionResult> Index() => View(await ruleService.GetOrderedAsync());
}
