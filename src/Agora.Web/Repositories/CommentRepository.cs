using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class CommentRepository(AgoraDbContext dbContext) : ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> GetForPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        await dbContext.Comments.AsNoTracking().Include(comment => comment.Author)
            .Where(comment => comment.PostId == postId && !comment.Post.IsRemoved && comment.Post.Forum.Status == ForumStatus.Approved)
            .OrderBy(comment => comment.CreatedAtUtc).ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

    public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().FirstOrDefaultAsync(post =>
            post.Id == postId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<Comment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Comments.FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await dbContext.Comments.AddAsync(comment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        dbContext.Comments.Remove(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
