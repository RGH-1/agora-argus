using Agora.Web.Repositories;

namespace Agora.Web.Services;

public interface ILikesService
{
    Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default);
    Task ToggleAsync(int postId, string userId, CancellationToken cancellationToken = default);
}
