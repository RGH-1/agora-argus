using Agora.Web.Repositories;

namespace Agora.Web.Services;

public sealed class LikesService(ILikesRepository likesRepository, IBanStatusService? banStatusService = null, Argus.Client.IArgusTracker? argusTracker = null) : ILikesService
{
    public Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default) =>
        likesRepository.GetStatusForPublicPostAsync(postId, userId, cancellationToken);

    public Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default) =>
        likesRepository.GetCountsForPostsAsync(postIds, cancellationToken);

    public async Task ToggleAsync(int postId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User ID is required.", nameof(userId));
        if (banStatusService is not null) await banStatusService.EnsureNotBannedAsync(userId, cancellationToken);
        var post = await likesRepository.GetPublicPostAsync(postId, cancellationToken) ?? throw new LikeNotFoundOrForbiddenException();
        if (await likesRepository.TryRemoveAsync(postId, userId, cancellationToken))
        { await ActivityTracking.TrackAsync(argusTracker, "post.unliked", post.Forum.Name, post.Id.ToString(), post.Title, userId); return; }
        if (await likesRepository.AddAsync(postId, userId, cancellationToken) == LikeAddResult.Added)
            await ActivityTracking.TrackAsync(argusTracker, "post.liked", post.Forum.Name, post.Id.ToString(), post.Title, userId);
    }
}

public sealed class LikeNotFoundOrForbiddenException : Exception;
