using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Agora.Web.Hubs;

[Authorize]
public sealed class AgoraNotificationHub(IServiceScopeFactory scopeFactory, UserManager<ApplicationUser> userManager) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) { Context.Abort(); return; }
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgoraDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, Context.ConnectionAborted);
        if (user is null || user.IsBanned) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}", Context.ConnectionAborted);
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains("Moderator") || roles.Contains("Admin"))
            await Groups.AddToGroupAsync(Context.ConnectionId, "moderators", Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
