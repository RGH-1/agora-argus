using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class UserManagementRepository(AgoraDbContext dbContext) : IUserManagementRepository
{
    public async Task<(IReadOnlyList<ApplicationUser> Users, bool HasNextPage)> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.DisplayName.Contains(term) || (user.UserName != null && user.UserName.Contains(term)) || (user.Email != null && user.Email.Contains(term)));
        }
        var users = await query.OrderBy(user => user.DisplayName).ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        return (users.Take(pageSize).ToList(), users.Count > pageSize);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetRolesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToArray();
        var memberships = await (from userRole in dbContext.UserRoles.AsNoTracking()
                      join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                      where ids.Contains(userRole.UserId)
                      select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(cancellationToken);
        return memberships.GroupBy(item => item.UserId).ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.Role).Order().ToList());
    }

    public Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<bool> TrySetBanStatusAsync(string targetUserId, string actorId, bool isBan, string reason, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var changed = await dbContext.Users.Where(user => user.Id == targetUserId && user.IsBanned != isBan)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsBanned, isBan), cancellationToken);
        if (changed != 1) return false;
        dbContext.UserBanActions.Add(new UserBanAction { TargetUserId = targetUserId, ActorId = actorId, IsBan = isBan, Reason = reason, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return true;
    }
}
