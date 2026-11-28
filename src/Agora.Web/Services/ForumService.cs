using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public class ForumService(IForumRepository forumRepository, IBanStatusService? banStatusService = null, Argus.Client.IArgusTracker? argusTracker = null, IRealtimeNotificationService? realtimeNotifications = null) : IForumService
{
    public async Task<Forum> CreateAsync(string name, string? description, string creatorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ForumBusinessRuleException("A forum name is required.");
        if (string.IsNullOrWhiteSpace(creatorId)) throw new ArgumentException("Creator ID is required.", nameof(creatorId));
        if (name.Trim().Length > 100) throw new ForumBusinessRuleException("Forum names cannot exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(description)) throw new ForumBusinessRuleException("A forum description is required.");
        if (description.Trim().Length > 500) throw new ForumBusinessRuleException("Forum descriptions cannot exceed 500 characters.");

        var user = await forumRepository.GetUserAsync(creatorId, cancellationToken)
            ?? throw new ForumBusinessRuleException("Your account could not be found.");
        if (user.IsBanned || banStatusService is not null && await IsBannedAsync(creatorId, cancellationToken)) throw new ForumBusinessRuleException("Banned accounts cannot create forums.");

        var now = DateTime.UtcNow;
        var forum = new Forum
        {
            Name = name.Trim(),
            NormalizedName = NormalizeName(name),
            Description = description.Trim(),
            CreatorId = creatorId,
            Status = ForumStatus.Pending,
            CreatedAtUtc = now,
            SubmittedAtUtc = now,
            UpdatedAtUtc = now
        };
        var result = await forumRepository.TryCreateAsync(forum, !user.IsPro, cancellationToken);
        var created = result.Failure switch
        {
            ForumCreateFailure.None => result.Forum!,
            ForumCreateFailure.DuplicateName => throw new ForumBusinessRuleException("A forum with this name already exists."),
            ForumCreateFailure.FreeLimitReached => throw new ForumBusinessRuleException("Free members can own up to 3 forums. Upgrade to Pro to create more."),
            _ => throw new InvalidOperationException("Unexpected forum creation outcome.")
        };
        await ActivityTracking.TrackAsync(argusTracker, "Forum.Added", created.Name, created.Id.ToString(), created.Name, creatorId);
        if (created.Status == ForumStatus.Pending) await NotifyAsync(() => realtimeNotifications?.PendingForumCreatedAsync(created.Id, created.Name));
        return created;
    }

    public Task<IReadOnlyList<Forum>> GetPublicAsync(CancellationToken cancellationToken = default) =>
        forumRepository.GetApprovedAsync(cancellationToken);

    public Task<Forum?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default) =>
        forumRepository.GetApprovedByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Forum>> GetMineAsync(string ownerId, CancellationToken cancellationToken = default) =>
        forumRepository.GetByOwnerAsync(ownerId, cancellationToken);

    public Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default) =>
        forumRepository.GetOwnedByIdAsync(id, ownerId, cancellationToken);

    public async Task<ForumDescriptionUpdateResult> UpdateDescriptionAsync(int id, string description, string ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ForumBusinessRuleException("A forum description is required.");
        if (description.Trim().Length > 500) throw new ForumBusinessRuleException("Forum descriptions cannot exceed 500 characters.");
        var result = await forumRepository.UpdateDescriptionIfChangedAsync(id, ownerId, description.Trim(), cancellationToken);
        if (result == ForumDescriptionUpdateResult.NotFoundOrForbidden) throw new ForumNotFoundOrForbiddenException();
        return result;
    }

    public async Task DeleteAsync(int id, string ownerId, CancellationToken cancellationToken = default)
    {
        var forum = await forumRepository.GetOwnedByIdAsync(id, ownerId, cancellationToken)
            ?? throw new ForumNotFoundOrForbiddenException();
        await forumRepository.DeleteAsync(forum, cancellationToken);
    }

    public Task<IReadOnlyList<Forum>> GetPendingAsync(CancellationToken cancellationToken = default) => forumRepository.GetPendingAsync(cancellationToken);

    public Task<Forum?> GetPendingByIdAsync(int id, CancellationToken cancellationToken = default) => forumRepository.GetPendingByIdAsync(id, cancellationToken);

    public async Task ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        var forum = await forumRepository.GetPendingByIdAsync(id, cancellationToken);
        if (forum is null || !await forumRepository.TryApproveAsync(id, cancellationToken))
            throw new ForumModerationException("This forum was already reviewed or no longer exists.");
        await ActivityTracking.TrackAsync(argusTracker, "forum.approved", forum.Name, forum.Id.ToString(), forum.Name);
        await NotifyAsync(() => realtimeNotifications?.ForumApprovedAsync(forum.CreatorId, forum.Id, forum.Name));
        await NotifyAsync(() => realtimeNotifications?.ModerationQueueChangedAsync("forums"));
    }

    public async Task RejectAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ForumBusinessRuleException("A rejection reason is required.");
        if (reason.Trim().Length > 500) throw new ForumBusinessRuleException("Rejection reasons cannot exceed 500 characters.");
        var forum = await forumRepository.GetPendingByIdAsync(id, cancellationToken);
        if (forum is null || !await forumRepository.TryRejectAsync(id, reason.Trim(), cancellationToken))
            throw new ForumModerationException("This forum was already reviewed or no longer exists.");
        await ActivityTracking.TrackAsync(argusTracker, "forum.rejected", forum.Name, forum.Id.ToString(), forum.Name);
        await NotifyAsync(() => realtimeNotifications?.ForumRejectedAsync(forum.CreatorId, forum.Id, forum.Name));
        await NotifyAsync(() => realtimeNotifications?.ModerationQueueChangedAsync("forums"));
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
    private static async Task NotifyAsync(Func<Task?> send) { var task = send(); if (task is not null) try { await task; } catch { } }
    private async Task<bool> IsBannedAsync(string userId, CancellationToken cancellationToken) { try { if (banStatusService is not null) await banStatusService.EnsureNotBannedAsync(userId, cancellationToken); return false; } catch (BannedUserException) { return true; } }
}

public sealed class ForumBusinessRuleException(string message) : Exception(message);
public sealed class ForumNotFoundOrForbiddenException : Exception;
public sealed class ForumModerationException(string message) : Exception(message);
