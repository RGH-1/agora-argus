using Argus.Web.Controllers;
using Argus.Web.Models.ClientApps;
using Argus.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.ViewComponents;

/// <summary>The client switcher in the layout (§4.1): a view component, because every page shows it with its own data.</summary>
public class ClientSwitcherViewComponent(IClientAppService clientAppService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var clients = await clientAppService.GetAllAsync(HttpContext.RequestAborted);
        var selected = ClientSelection.Resolve(HttpContext, clients, Request.Query["clientId"].FirstOrDefault());

        // Back to the same page without its ?clientId=, which would otherwise win over the new choice.
        var query = QueryString.Create(Request.Query.Where(pair => pair.Key != "clientId"));
        return View(new ClientSwitcherViewModel(clients, selected, $"{Request.PathBase}{Request.Path}{query}"));
    }
}