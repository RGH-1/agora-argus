using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;

namespace Agora.Web.Services;

public interface IReportService
{
    Task<Post> GetReportablePostAsync(int postId, string reporterId, CancellationToken cancellationToken = default);
    Task<Report> CreateAsync(int postId, string category, string? details, string reporterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default);
    Task ResolveAsync(int reportId, string moderatorId, ModerationActionType actionType, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModerationHistoryItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserWarning>> GetWarningsForUserAsync(string userId, CancellationToken cancellationToken = default);
}
