using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface ISubscriptionRepository
{
    Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> TryUpgradeToProAsync(string userId, CancellationToken cancellationToken = default);
}
