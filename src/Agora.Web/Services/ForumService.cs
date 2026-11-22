using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public class ForumService(IForumRepository forumRepository) : IForumService
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

        var now = DateTime.UtcNow;
        var forum = new Forum
        {
            Name = name.Trim(),
            NormalizedName = NormalizeName(name),
            Description = description.Trim(),
            CreatorId = creatorId,
            Status = ForumStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var result = await forumRepository.TryCreateAsync(forum, !user.IsPro, cancellationToken);
        return result.Failure switch
        {
            ForumCreateFailure.None => result.Forum!,
            ForumCreateFailure.DuplicateName => throw new ForumBusinessRuleException("A forum with this name already exists."),
            ForumCreateFailure.FreeLimitReached => throw new ForumBusinessRuleException("Free members can own up to 3 forums. Upgrade to Pro to create more."),
            _ => throw new InvalidOperationException("Unexpected forum creation outcome.")
        };
    }

    public Task<IReadOnlyList<Forum>> GetPublicAsync(CancellationToken cancellationToken = default) =>
        forumRepository.GetApprovedAsync(cancellationToken);

    public Task<Forum?> GetPublicByIdAsync(int id, CancellationToken cancellationToken = default) =>
        forumRepository.GetApprovedByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Forum>> GetMineAsync(string ownerId, CancellationToken cancellationToken = default) =>
        forumRepository.GetByOwnerAsync(ownerId, cancellationToken);

    public Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default) =>
        forumRepository.GetOwnedByIdAsync(id, ownerId, cancellationToken);

    public async Task UpdateDescriptionAsync(int id, string description, string ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ForumBusinessRuleException("A forum description is required.");
        if (description.Trim().Length > 500) throw new ForumBusinessRuleException("Forum descriptions cannot exceed 500 characters.");
        var forum = await forumRepository.GetOwnedByIdAsync(id, ownerId, cancellationToken)
            ?? throw new ForumNotFoundOrForbiddenException();
        forum.Description = description.Trim();
        await forumRepository.UpdateAsync(forum, cancellationToken);
    }

    public async Task DeleteAsync(int id, string ownerId, CancellationToken cancellationToken = default)
    {
        var forum = await forumRepository.GetOwnedByIdAsync(id, ownerId, cancellationToken)
            ?? throw new ForumNotFoundOrForbiddenException();
        await forumRepository.DeleteAsync(forum, cancellationToken);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}

public sealed class ForumBusinessRuleException(string message) : Exception(message);
public sealed class ForumNotFoundOrForbiddenException : Exception;
