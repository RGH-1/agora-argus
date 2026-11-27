using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class LikesRepository(AgoraDbContext dbContext) : ILikesRepository
{
    public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().Include(post => post.Forum).FirstOrDefaultAsync(post =>
            post.Id == postId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking()
            .Where(post => post.Id == postId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved)
            .Select(post => new LikeStatus(post.Likes.Count(), userId != null && post.Likes.Any(like => like.UserId == userId)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToArray();
        return await dbContext.Likes.AsNoTracking().Where(like => ids.Contains(like.PostId))
            .GroupBy(like => like.PostId).Select(group => new { PostId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);
    }

    public async Task<bool> TryRemoveAsync(int postId, string userId, CancellationToken cancellationToken = default) =>
        await dbContext.Likes.Where(like => like.PostId == postId && like.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<LikeAddResult> AddAsync(int postId, string userId, CancellationToken cancellationToken = default)
    {
        var like = new Like { PostId = postId, UserId = userId, CreatedAtUtc = DateTime.UtcNow };
        await dbContext.Likes.AddAsync(like, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return LikeAddResult.Added;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(like).State = EntityState.Detached;
            return LikeAddResult.AlreadyExists;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
           && exception.InnerException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
}
