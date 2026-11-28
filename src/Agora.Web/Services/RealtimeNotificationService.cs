using Agora.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Agora.Web.Services;

public sealed class RealtimeNotificationService(IHubContext<AgoraNotificationHub> hub, ILogger<RealtimeNotificationService> logger) : IRealtimeNotificationService
{
    public Task PendingForumCreatedAsync(int forumId, string forumName) => SendModeratorsAsync(new("PendingForumCreated", $"New forum submission: {forumName}", "/Moderation/Forums"));
    public Task ForumApprovedAsync(string ownerId, int forumId, string forumName) => SendUserAsync(ownerId, new("ForumApproved", $"Your forum '{forumName}' has been approved!", $"/Forums/Details/{forumId}"));
    public Task ForumRejectedAsync(string ownerId, int forumId, string forumName) => SendUserAsync(ownerId, new("ForumRejected", $"Your forum '{forumName}' was rejected. View the reason in My Forums.", "/Forums/Mine"));
    public Task PendingReportCreatedAsync(int reportId) => SendModeratorsAsync(new("PendingReportCreated", "A new report is waiting for review.", "/Moderation/Reports"));
    public Task PostRemovedAsync(string authorId, int postId, string postTitle) => SendUserAsync(authorId, new("PostRemoved", $"Your post '{postTitle}' was removed by moderation."));
    public Task WarningIssuedAsync(string userId) => SendUserAsync(userId, new("ModerationWarning", "You received a moderation warning. View your profile for details.", "/Account/Profile"));
    public Task UserBannedAsync(string userId) => SendUserAsync(userId, new("AccountBanned", "Your account has been banned by moderation."));
    public Task ModerationQueueChangedAsync(string queue) => SendModeratorsAsync(new("ModerationQueueChanged", queue));
    private async Task SendUserAsync(string userId, RealtimeNotification notification) { try { await hub.Clients.Group($"user:{userId}").SendAsync("ReceiveNotification", notification); } catch (Exception exception) { logger.LogWarning(exception, "Realtime notification delivery failed for user {UserId}", userId); } }
    private async Task SendModeratorsAsync(RealtimeNotification notification) { try { await hub.Clients.Group("moderators").SendAsync("ReceiveNotification", notification); } catch (Exception exception) { logger.LogWarning(exception, "Realtime moderator notification delivery failed"); } }
}
