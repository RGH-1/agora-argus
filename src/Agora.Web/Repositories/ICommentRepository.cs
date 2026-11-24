using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetForPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Comment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default);
}
