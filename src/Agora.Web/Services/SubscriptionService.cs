using Agora.Web.Repositories;
using Argus.Client;

namespace Agora.Web.Services;

public sealed class SubscriptionService(ISubscriptionRepository subscriptionRepository, IArgusTracker argusTracker, ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public const decimal ProPrice = 4.99m;

    public async Task<SubscriptionUpgradeResult> UpgradeToProAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new(SubscriptionUpgradeOutcome.NotFound);
        var user = await subscriptionRepository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(SubscriptionUpgradeOutcome.NotFound);
        if (user.IsBanned) return new(SubscriptionUpgradeOutcome.Banned);
        if (user.IsPro) return new(SubscriptionUpgradeOutcome.AlreadyPro);

        if (!await subscriptionRepository.TryUpgradeToProAsync(userId, cancellationToken))
        {
            var currentUser = await subscriptionRepository.GetUserAsync(userId, cancellationToken);
            return new(currentUser switch { null => SubscriptionUpgradeOutcome.NotFound, { IsBanned: true } => SubscriptionUpgradeOutcome.Banned, _ => SubscriptionUpgradeOutcome.AlreadyPro });
        }

        try { await argusTracker.TrackAsync("subscription.purchased", value: ProPrice, actorId: userId); }
        catch (Exception exception) { logger.LogWarning(exception, "Could not send subscription purchase activity for user {UserId}", userId); }
        return new(SubscriptionUpgradeOutcome.Succeeded);
    }
}
