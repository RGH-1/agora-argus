using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IReportRepository
{
    Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task<bool> HasOpenReportAsync(int postId, string reportingUserId, CancellationToken cancellationToken = default);
    Task<ReportAddResult> AddAsync(Report report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default);
}

public enum ReportAddResult { Added, DuplicateOpenReport }
