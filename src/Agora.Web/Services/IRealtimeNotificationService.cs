namespace Agora.Web.Services;

public interface IRealtimeNotificationService
{
    Task PendingForumCreatedAsync(int forumId, string forumName);
    Task ForumApprovedAsync(string ownerId, int forumId, string forumName);
    Task ForumRejectedAsync(string ownerId, int forumId, string forumName);
    Task PendingReportCreatedAsync(int reportId);
    Task PostRemovedAsync(string authorId, int postId, string postTitle);
    Task WarningIssuedAsync(string userId);
    Task UserBannedAsync(string userId);
    Task ModerationQueueChangedAsync(string queue);
}

public sealed record RealtimeNotification(string Type, string Message, string? Url = null);
