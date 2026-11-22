using Agora.Web.Data.Entities;

namespace Agora.Web.Services;

public interface IForumService
{
    Task<Forum> CreateAsync(string name, string? description, string creatorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetPublicAsync(CancellationToken cancellationToken = default);
    Task<Forum?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetMineAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default);
    Task UpdateDescriptionAsync(int id, string description, string ownerId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, string ownerId, CancellationToken cancellationToken = default);
}
