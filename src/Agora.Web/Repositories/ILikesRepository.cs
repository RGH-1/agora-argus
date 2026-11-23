using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface ILikesRepository
{
    Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default);
    Task<bool> TryRemoveAsync(int postId, string userId, CancellationToken cancellationToken = default);
    Task<LikeAddResult> AddAsync(int postId, string userId, CancellationToken cancellationToken = default);
}

public sealed record LikeStatus(int Count, bool IsLiked);
public enum LikeAddResult { Added, AlreadyExists }
