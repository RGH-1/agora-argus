using Agora.Web.Models.Moderation;

namespace Agora.Web.Services;

public interface IUserManagementService
{
    Task<UserManagementPageViewModel> GetPageAsync(string? search, int page, CancellationToken cancellationToken = default);
    Task<UserBanFormViewModel?> GetBanFormAsync(string userId, CancellationToken cancellationToken = default);
    Task ChangeBanAsync(string targetUserId, string actorId, bool actorIsAdmin, bool isBan, string reason, CancellationToken cancellationToken = default);
    Task ChangeModeratorRoleAsync(string targetUserId, string actorId, bool grant, CancellationToken cancellationToken = default);
}
