using Argus.Client;

namespace Agora.Web.Services;

internal static class ActivityTracking
{
    public static async Task TrackAsync(IArgusTracker? tracker, string eventType, string? dimension = null, string? targetId = null, string? targetLabel = null, string? actorId = null)
    {
        if (tracker is null) return;
        try { await tracker.TrackAsync(eventType, 1m, dimension, targetId, targetLabel, actorId); }
        catch { /* Argus delivery is best-effort and must not undo persisted Agora work. */ }
    }
}
