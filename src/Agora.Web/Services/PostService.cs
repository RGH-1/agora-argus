using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public class PostService(IPostRepository postRepository, IBanStatusService? banStatusService = null, Argus.Client.IArgusTracker? argusTracker = null) : IPostService
{
    public async Task<Post> CreateAsync(int forumId, string title, string content, string authorId, CancellationToken cancellationToken = default)
    {
        Validate(title, content);
        if (string.IsNullOrWhiteSpace(authorId)) throw new ArgumentException("Author ID is required.", nameof(authorId));
        if (banStatusService is not null) await banStatusService.EnsureNotBannedAsync(authorId, cancellationToken);
        var forum = await postRepository.GetForumAsync(forumId, cancellationToken) ?? throw new PostNotFoundOrForbiddenException();
        if (forum.Status != ForumStatus.Approved) throw new PostBusinessRuleException("Posts can only be created in approved forums.");
        var now = DateTime.UtcNow;
        var post = new Post { ForumId = forumId, AuthorId = authorId, Title = title.Trim(), Content = content.Trim(), CreatedAtUtc = now, UpdatedAtUtc = now };
        await postRepository.AddAsync(post, cancellationToken);
        await ActivityTracking.TrackAsync(argusTracker, "Post.Added", forum.Name, post.Id.ToString(), post.Title, authorId);
        return post;
    }

    public async Task EnsureForumApprovedAsync(int forumId, CancellationToken cancellationToken = default)
    {
        var forum = await postRepository.GetForumAsync(forumId, cancellationToken) ?? throw new PostNotFoundOrForbiddenException();
        if (forum.Status != ForumStatus.Approved) throw new PostBusinessRuleException("Posts can only be created in approved forums.");
    }

    public Task<IReadOnlyList<Post>> GetPublicByForumAsync(int forumId, CancellationToken cancellationToken = default) => postRepository.GetPublicByForumAsync(forumId, cancellationToken);
    public Task<Post?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default) => postRepository.GetPublicByIdAsync(id, cancellationToken);
    public Task<Post?> GetOwnedActiveAsync(int id, string authorId, CancellationToken cancellationToken = default) => postRepository.GetOwnedActiveAsync(id, authorId, cancellationToken);

    public async Task UpdateAsync(int id, string title, string content, string authorId, CancellationToken cancellationToken = default)
    {
        Validate(title, content);
        if (banStatusService is not null) await banStatusService.EnsureNotBannedAsync(authorId, cancellationToken);
        var post = await postRepository.GetOwnedActiveAsync(id, authorId, cancellationToken) ?? throw new PostNotFoundOrForbiddenException();
        post.Title = title.Trim();
        post.Content = content.Trim();
        await postRepository.UpdateAsync(post, cancellationToken);
    }

    public async Task RemoveAsync(int id, string authorId, CancellationToken cancellationToken = default)
    {
        var post = await postRepository.GetOwnedActiveAsync(id, authorId, cancellationToken) ?? throw new PostNotFoundOrForbiddenException();
        post.IsRemoved = true;
        await postRepository.UpdateAsync(post, cancellationToken);
        await ActivityTracking.TrackAsync(argusTracker, "content.removed", post.Forum.Name, post.Id.ToString(), post.Title, authorId);
    }

    private static void Validate(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new PostBusinessRuleException("A post title is required.");
        if (title.Trim().Length > 150) throw new PostBusinessRuleException("Post titles cannot exceed 150 characters.");
        if (string.IsNullOrWhiteSpace(content)) throw new PostBusinessRuleException("Post content is required.");
        if (content.Trim().Length > 4000) throw new PostBusinessRuleException("Post content cannot exceed 4000 characters.");
    }
}

public sealed class PostBusinessRuleException(string message) : Exception(message);
public sealed class PostNotFoundOrForbiddenException : Exception;
