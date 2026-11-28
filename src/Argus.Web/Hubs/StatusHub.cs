using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Argus.Web.Hubs;

/// <summary>
/// Pushes health changes to every open Argus page (§4.2). Only the server sends (through IStatusBoardNotifier),
/// so the hub has no methods for the browser to call.
/// </summary>
[Authorize] // the global AuthorizeFilter only covers MVC controllers, not hubs
public class StatusHub : Hub
{
}