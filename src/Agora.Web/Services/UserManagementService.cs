using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;
using Agora.Web.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Agora.Web.Services;

public sealed class UserManagementService(IUserManagementRepository repository, UserManager<ApplicationUser> userManager, Argus.Client.IArgusTracker? argusTracker = null, IRealtimeNotificationService? realtimeNotifications = null) : IUserManagementService
{
    private const int PageSize = 25;
    public async Task<UserManagementPageViewModel> GetPageAsync(string? search, int page, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); var (users, hasNext) = await repository.GetPageAsync(search, page, PageSize, cancellationToken);
        var roles = await repository.GetRolesAsync(users.Select(user => user.Id), cancellationToken);
        return new UserManagementPageViewModel { Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), Page = page, HasNextPage = hasNext, Users = users.Select(user => new ManagedUserViewModel { Id = user.Id, DisplayName = user.DisplayName, UserName = user.UserName, Email = user.Email, IsBanned = user.IsBanned, Roles = roles.GetValueOrDefault(user.Id, []) }).ToList() };
    }
    public async Task<UserBanFormViewModel?> GetBanFormAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken); return user is null ? null : new UserBanFormViewModel { UserId = user.Id, DisplayName = user.DisplayName, UserName = user.UserName, Email = user.Email };
    }
    public async Task ChangeBanAsync(string targetUserId, string actorId, bool actorIsAdmin, bool isBan, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new UserManagementException("A reason of up to 500 characters is required.");
        if (targetUserId == actorId) throw new UserManagementException("You cannot ban or unban your own account.");
        var target = await repository.GetByIdAsync(targetUserId, cancellationToken) ?? throw new UserManagementException("The selected user no longer exists.");
        if (await userManager.IsInRoleAsync(target, "Admin")) throw new UserManagementException("Admin accounts cannot be banned or unbanned here.");
        if (!actorIsAdmin && await userManager.IsInRoleAsync(target, "Moderator")) throw new UserManagementException("Only an Admin can manage a Moderator account.");
        if (!await repository.TrySetBanStatusAsync(targetUserId, actorId, isBan, reason.Trim(), cancellationToken)) throw new UserManagementException(isBan ? "This user is already banned." : "This user is already active.");
        var updatedTarget = await userManager.FindByIdAsync(targetUserId) ?? throw new UserManagementException("The selected user no longer exists.");
        await userManager.UpdateSecurityStampAsync(updatedTarget);
        if (isBan) { await ActivityTracking.TrackAsync(argusTracker, "user.banned", targetId: targetUserId, actorId: actorId); if (realtimeNotifications is not null) try { await realtimeNotifications.UserBannedAsync(targetUserId); } catch { } }
    }
    public async Task ChangeModeratorRoleAsync(string targetUserId, string actorId, bool grant, CancellationToken cancellationToken = default)
    {
        if (targetUserId == actorId && !grant) throw new UserManagementException("You cannot remove your own Moderator role.");
        var target = await userManager.FindByIdAsync(targetUserId) ?? throw new UserManagementException("The selected user no longer exists.");
        if (await userManager.IsInRoleAsync(target, "Admin")) throw new UserManagementException("Admin roles cannot be changed here.");
        var isModerator = await userManager.IsInRoleAsync(target, "Moderator");
        if (grant == isModerator) throw new UserManagementException(grant ? "This user is already a Moderator." : "This user is not a Moderator.");
        var result = grant ? await userManager.AddToRoleAsync(target, "Moderator") : await userManager.RemoveFromRoleAsync(target, "Moderator");
        if (!result.Succeeded) throw new UserManagementException("The role change could not be completed.");
        await userManager.UpdateSecurityStampAsync(target);
    }
}
public sealed class UserManagementException(string message) : Exception(message);
