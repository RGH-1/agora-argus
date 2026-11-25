using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;

namespace Agora.Web.Repositories;

public interface IReportRepository
{
    Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<bool> HasOpenReportAsync(int postId, string reportingUserId, CancellationToken cancellationToken = default);
    Task<ReportAddResult> AddAsync(Report report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default);
    Task<Report?> GetOpenForModerationAsync(int reportId, CancellationToken cancellationToken = default);
    Task<Report?> GetOpenForAiCheckAsync(int reportId, CancellationToken cancellationToken = default);
    Task<bool> TrySaveAiAssessmentAsync(int reportId, string assessment, decimal confidence, DateTime assessedAtUtc, CancellationToken cancellationToken = default);
    Task<ModerationResolutionResult> ResolveAsync(int reportId, string moderatorId, ModerationActionType actionType, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModerationHistoryItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserWarning>> GetWarningsForUserAsync(string userId, CancellationToken cancellationToken = default);
}

public enum ReportAddResult { Added, DuplicateOpenReport }
public enum ModerationResolutionResult { Resolved, AlreadyHandled, NotFound, AlreadyBanned }
