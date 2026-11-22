using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public class ForumService(IForumRepository forumRepository) : IForumService
{
    public async Task<Forum> CreateAsync(string name, string? description, string creatorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Forum name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(creatorId)) throw new ArgumentException("Creator ID is required.", nameof(creatorId));

        var now = DateTime.UtcNow;
        var forum = new Forum
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatorId = creatorId,
            Status = ForumStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await forumRepository.AddAsync(forum, cancellationToken);
        return forum;
    }

    public Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        forumRepository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken cancellationToken = default) =>
        forumRepository.GetAllAsync(cancellationToken);
}
