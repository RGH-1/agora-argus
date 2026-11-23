using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public class PostRepository(AgoraDbContext dbContext) : IPostRepository
{
    public Task<Forum?> GetForumAsync(int forumId, CancellationToken cancellationToken = default) =>
        dbContext.Forums.FirstOrDefaultAsync(forum => forum.Id == forumId, cancellationToken);

    public async Task<IReadOnlyList<Post>> GetPublicByForumAsync(int forumId, CancellationToken cancellationToken = default) =>
        await dbContext.Posts.AsNoTracking().Include(post => post.Author)
            .Where(post => post.ForumId == forumId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved)
            .OrderByDescending(post => post.CreatedAtUtc).ThenByDescending(post => post.Id).ToListAsync(cancellationToken);

    public Task<Post?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().Include(post => post.Author).Include(post => post.Forum)
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public Task<Post?> GetOwnedActiveAsync(int id, string authorId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.Include(post => post.Forum)
            .FirstOrDefaultAsync(post => post.Id == id && post.AuthorId == authorId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        await dbContext.Posts.AddAsync(post, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        post.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
