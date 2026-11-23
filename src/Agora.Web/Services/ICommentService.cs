using Agora.Web.Data.Entities;

namespace Agora.Web.Services;

public interface ICommentService
{
    Task<IReadOnlyList<Comment>> GetForPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<Comment> CreateAsync(int postId, string content, string authorId, CancellationToken cancellationToken = default);
    Task<int> DeleteAsync(int commentId, string requestorId, bool isModerator, CancellationToken cancellationToken = default);
}
