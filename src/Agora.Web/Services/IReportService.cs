using Agora.Web.Data.Entities;

namespace Agora.Web.Services;

public interface IReportService
{
    Task<Post> GetReportablePostAsync(int postId, string reporterId, CancellationToken cancellationToken = default);
    Task<Report> CreateAsync(int postId, string category, string? details, string reporterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default);
}
