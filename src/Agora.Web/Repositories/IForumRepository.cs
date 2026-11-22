using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IForumRepository
{
    Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Forum forum, CancellationToken cancellationToken = default);
    Task UpdateAsync(Forum forum, CancellationToken cancellationToken = default);
}
