using Agora.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Services;

public sealed class BanStatusService(AgoraDbContext dbContext) : IBanStatusService
{
    public async Task EnsureNotBannedAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsBanned, cancellationToken)) throw new BannedUserException();
    }
}
