namespace Agora.Web.Services;

public interface ISubscriptionService
{
    Task<SubscriptionUpgradeResult> UpgradeToProAsync(string userId, CancellationToken cancellationToken = default);
}

public enum SubscriptionUpgradeOutcome { Succeeded, AlreadyPro, Banned, NotFound }
public readonly record struct SubscriptionUpgradeResult(SubscriptionUpgradeOutcome Outcome) { public bool Succeeded => Outcome == SubscriptionUpgradeOutcome.Succeeded; }
