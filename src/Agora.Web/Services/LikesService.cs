using Agora.Web.Repositories;

namespace Agora.Web.Services;

public sealed class LikesService(ILikesRepository likesRepository) : ILikesService
{
    public Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default) =>
        likesRepository.GetStatusForPublicPostAsync(postId, userId, cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default) =>
        likesRepository.GetCountsForPostsAsync(postIds, cancellationToken);

    public async Task ToggleAsync(int postId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));
        if (await likesRepository.GetPublicPostAsync(postId, cancellationToken) is null) throw new LikeNotFoundOrForbiddenException();
        if (await likesRepository.TryRemoveAsync(postId, userId, cancellationToken)) return;
        await likesRepository.AddAsync(postId, userId, cancellationToken);
    }
}

public sealed class LikeNotFoundOrForbiddenException : Exception;
