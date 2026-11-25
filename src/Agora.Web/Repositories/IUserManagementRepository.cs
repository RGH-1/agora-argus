using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IUserManagementRepository
{
    Task<(IReadOnlyList<ApplicationUser> Users, bool HasNextPage)> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetRolesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> TrySetBanStatusAsync(string targetUserId, string actorId, bool isBan, string reason, CancellationToken cancellationToken = default);
}
