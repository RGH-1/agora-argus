using Agora.Web.Data.Entities;

namespace Agora.Web.Services;

public interface IForumService
{
    Task<Forum> CreateAsync(string name, string? description, string creatorId, CancellationToken cancellationToken = default);
    Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken cancellationToken = default);
}
