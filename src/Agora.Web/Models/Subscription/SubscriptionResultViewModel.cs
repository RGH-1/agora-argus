namespace Agora.Web.Models.Subscription;
public sealed class SubscriptionResultViewModel
{
    public bool Succeeded { get; init; }
    public bool AlreadyPro { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime? TransactionTimeUtc { get; init; }
    public string? Reference { get; init; }
}
