using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Agora.Web.Repositories;

public class ForumRepository(AgoraDbContext dbContext) : IForumRepository
{
    public Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Forums.Include(forum => forum.Creator).FirstOrDefaultAsync(forum => forum.Id == id, cancellationToken);

    public Task<Forum?> GetApprovedByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Forums.AsNoTracking().Include(forum => forum.Creator)
            .FirstOrDefaultAsync(forum => forum.Id == id && forum.Status == ForumStatus.Approved, cancellationToken);

    public async Task<IReadOnlyList<Forum>> GetApprovedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Forums.AsNoTracking().Include(forum => forum.Creator)
            .Where(forum => forum.Status == ForumStatus.Approved).OrderBy(forum => forum.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Forum>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default) =>
        await dbContext.Forums.AsNoTracking().Where(forum => forum.CreatorId == ownerId)
            .OrderByDescending(forum => forum.UpdatedAtUtc).ToListAsync(cancellationToken);

    public Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default) =>
        dbContext.Forums.FirstOrDefaultAsync(forum => forum.Id == id && forum.CreatorId == ownerId, cancellationToken);

    public Task<Forum?> GetPendingByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Forums.AsNoTracking().Include(forum => forum.Creator)
            .FirstOrDefaultAsync(forum => forum.Id == id && forum.Status == ForumStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<Forum>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Forums.AsNoTracking().Include(forum => forum.Creator)
            .Where(forum => forum.Status == ForumStatus.Pending)
            .OrderBy(forum => forum.SubmittedAtUtc).ThenBy(forum => forum.Id).ToListAsync(cancellationToken);

    public Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<ForumCreateResult> TryCreateAsync(Forum forum, bool enforceFreeLimit, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (await dbContext.Forums.AnyAsync(item => item.NormalizedName == forum.NormalizedName, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ForumCreateResult(null, ForumCreateFailure.DuplicateName);
            }

            if (enforceFreeLimit && await dbContext.Forums.CountAsync(item => item.CreatorId == forum.CreatorId, cancellationToken) >= 3)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ForumCreateResult(null, ForumCreateFailure.FreeLimitReached);
            }

            await dbContext.Forums.AddAsync(forum, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ForumCreateResult(forum, ForumCreateFailure.None);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ForumCreateResult(null, ForumCreateFailure.DuplicateName);
        }
    }

    public async Task<ForumDescriptionUpdateResult> UpdateDescriptionIfChangedAsync(int id, string ownerId, string description, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var affected = await dbContext.Forums
            .Where(forum => forum.Id == id && forum.CreatorId == ownerId && forum.Description != description)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(forum => forum.Description, description)
                .SetProperty(forum => forum.Status, forum => forum.Status == ForumStatus.Approved || forum.Status == ForumStatus.Rejected ? ForumStatus.Pending : forum.Status)
                .SetProperty(forum => forum.RejectionReason, forum => forum.Status == ForumStatus.Rejected ? null : forum.RejectionReason)
                .SetProperty(forum => forum.SubmittedAtUtc, forum => forum.Status == ForumStatus.Approved || forum.Status == ForumStatus.Rejected ? now : forum.SubmittedAtUtc)
                .SetProperty(forum => forum.UpdatedAtUtc, now), cancellationToken);
        if (affected == 1) return ForumDescriptionUpdateResult.Updated;
        return await dbContext.Forums.AnyAsync(forum => forum.Id == id && forum.CreatorId == ownerId, cancellationToken)
            ? ForumDescriptionUpdateResult.Unchanged
            : ForumDescriptionUpdateResult.NotFoundOrForbidden;
    }

    public async Task DeleteAsync(Forum forum, CancellationToken cancellationToken = default)
    {
        dbContext.Forums.Remove(forum);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryApproveAsync(int id, CancellationToken cancellationToken = default) =>
        await dbContext.Forums.Where(forum => forum.Id == id && forum.Status == ForumStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(forum => forum.Status, ForumStatus.Approved)
                .SetProperty(forum => forum.RejectionReason, (string?)null)
                .SetProperty(forum => forum.UpdatedAtUtc, DateTime.UtcNow), cancellationToken) == 1;

    public async Task<bool> TryRejectAsync(int id, string rejectionReason, CancellationToken cancellationToken = default) =>
        await dbContext.Forums.Where(forum => forum.Id == id && forum.Status == ForumStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(forum => forum.Status, ForumStatus.Rejected)
                .SetProperty(forum => forum.RejectionReason, rejectionReason)
                .SetProperty(forum => forum.UpdatedAtUtc, DateTime.UtcNow), cancellationToken) == 1;
}
