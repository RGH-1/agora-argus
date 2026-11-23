using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IPostRepository
{
    Task<Forum?> GetForumAsync(int forumId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Post>> GetPublicByForumAsync(int forumId, CancellationToken cancellationToken = default);
    Task<Post?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Post?> GetOwnedActiveAsync(int id, string authorId, CancellationToken cancellationToken = default);
    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);
}
