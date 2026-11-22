using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IForumRepository
{
    Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Forum?> GetApprovedByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetApprovedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Forum>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<Forum?> GetOwnedByIdAsync(int id, string ownerId, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<ForumCreateResult> TryCreateAsync(Forum forum, bool enforceFreeLimit, CancellationToken cancellationToken = default);
    Task UpdateAsync(Forum forum, CancellationToken cancellationToken = default);
    Task DeleteAsync(Forum forum, CancellationToken cancellationToken = default);
}

public enum ForumCreateFailure { None, DuplicateName, FreeLimitReached }
public sealed record ForumCreateResult(Forum? Forum, ForumCreateFailure Failure);
