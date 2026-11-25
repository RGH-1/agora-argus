using Argus.Contracts;
using Argus.Contracts.Analytics;

namespace Argus.Client;

/// <summary>
/// Sends the client's own events to Argus, where each one becomes a data point the metric builder can chart (§4.3).
/// Inject it anywhere: <c>await argus.TrackAsync("post.liked", dimension: forum.Name, targetId: post.Id.ToString(), targetLabel: post.Title);</c>
/// </summary>
public interface IArgusTracker
{
    /// <param name="eventType">What happened, e.g. "post.liked". Becomes the data point's name in Argus.</param>
    /// <param name="value">1 for a plain count; an amount for sums, e.g. 4.99 for a payment.</param>
    /// <param name="dimension">What to group by, e.g. the forum name.</param>
    /// <param name="targetId">The item it happened to, e.g. a post id, for "top 5" charts.</param>
    /// <param name="actorId">Who did it (your own user id), if known.</param>
    Task TrackAsync(string eventType, decimal value = 1, string? dimension = null,
        string? targetId = null, string? targetLabel = null, string? actorId = null);
}

/// <summary>Never throws because of RabbitMQ: like heartbeats, an event is logged and dropped while the broker is down.</summary>
public sealed class ArgusTracker(ArgusSender sender) : IArgusTracker
{
    public Task TrackAsync(string eventType, decimal value = 1, string? dimension = null,
        string? targetId = null, string? targetLabel = null, string? actorId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType); // a bug in the calling code, not an outage

        return sender.SendAsync(ArgusRoutingKeys.ActivityRecorded, new ActivityRecorded
        {
            EventType = eventType,
            Value = value,
            Dimension = dimension,
            TargetId = targetId,
            TargetLabel = targetLabel,
            ActorId = actorId,
        });
    }
}