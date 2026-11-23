using Agora.Web.Data.Entities;

namespace Agora.Web.Services;

public interface IPostService
{
    Task<Post> CreateAsync(int forumId, string title, string content, string authorId, CancellationToken cancellationToken = default);
    Task EnsureForumApprovedAsync(int forumId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Post>> GetPublicByForumAsync(int forumId, CancellationToken cancellationToken = default);
    Task<Post?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Post?> GetOwnedActiveAsync(int id, string authorId, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string title, string content, string authorId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int id, string authorId, CancellationToken cancellationToken = default);
}
