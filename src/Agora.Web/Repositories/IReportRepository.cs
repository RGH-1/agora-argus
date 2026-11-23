using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IReportRepository
{
    Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default);
    Task AddAsync(Report report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default);
}
