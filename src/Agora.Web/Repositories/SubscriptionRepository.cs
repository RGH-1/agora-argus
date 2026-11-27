using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class SubscriptionRepository(AgoraDbContext dbContext) : ISubscriptionRepository
{
    public Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<bool> TryUpgradeToProAsync(string userId, CancellationToken cancellationToken = default) =>
        await dbContext.Users.Where(user => user.Id == userId && !user.IsPro && !user.IsBanned)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsPro, true), cancellationToken) == 1;
}
