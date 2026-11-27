using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public sealed class CommentService(ICommentRepository commentRepository, Argus.Client.IArgusTracker? argusTracker = null) : ICommentService
{
    public Task<IReadOnlyList<Comment>> GetForPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        commentRepository.GetForPublicPostAsync(postId, cancellationToken);

    public async Task<Comment> CreateAsync(int postId, string content, string authorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new CommentBusinessRuleException("Comment content is required.");
        if (content.Trim().Length > 2000) throw new CommentBusinessRuleException("Comments cannot exceed 2000 characters.");
        if (string.IsNullOrWhiteSpace(authorId)) throw new ArgumentException("Author ID is required.", nameof(authorId));
        var post = await commentRepository.GetPublicPostAsync(postId, cancellationToken) ?? throw new CommentNotFoundOrForbiddenException();
        var author = await commentRepository.GetUserAsync(authorId, cancellationToken) ?? throw new CommentNotFoundOrForbiddenException();
        if (author.IsBanned) throw new CommentBusinessRuleException("Banned users cannot add comments.");

        var now = DateTime.UtcNow;
        var comment = new Comment { PostId = postId, AuthorId = authorId, Content = content.Trim(), CreatedAtUtc = now, UpdatedAtUtc = now };
        await commentRepository.AddAsync(comment, cancellationToken);
        await ActivityTracking.TrackAsync(argusTracker, "Comment.Added", post.Forum.Name, post.Id.ToString(), post.Title, authorId);
        return comment;
    }

    public async Task<int> DeleteAsync(int commentId, string requestorId, bool isModerator, CancellationToken cancellationToken = default)
    {
        var comment = await commentRepository.GetByIdAsync(commentId, cancellationToken) ?? throw new CommentNotFoundOrForbiddenException();
        if (!isModerator && comment.AuthorId != requestorId) throw new CommentNotFoundOrForbiddenException();
        await commentRepository.DeleteAsync(comment, cancellationToken);
        return comment.PostId;
    }
}

public sealed class CommentBusinessRuleException(string message) : Exception(message);
public sealed class CommentNotFoundOrForbiddenException : Exception;
