using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public interface IForumService
{
    Task<Forum> CreateAsync(string name, string? description, string creatorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetPublicAsync(CancellationToken cancellationToken = default);
    Task<Forum?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetMineAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default);
    Task<ForumDescriptionUpdateResult> UpdateDescriptionAsync(int id, string description, string ownerId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, string ownerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<Forum?> GetPendingByIdAsync(int id, CancellationToken cancellationToken = default);
    Task ApproveAsync(int id, CancellationToken cancellationToken = default);
    Task RejectAsync(int id, string reason, CancellationToken cancellationToken = default);
}
