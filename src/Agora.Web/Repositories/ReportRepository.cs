using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class ReportRepository(AgoraDbContext dbContext) : IReportRepository
{
    public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().FirstOrDefaultAsync(post =>
            post.Id == postId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public async Task AddAsync(Report report, CancellationToken cancellationToken = default)
    {
        await dbContext.Reports.AddAsync(report, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Reports.AsNoTracking().Include(report => report.Post).Include(report => report.ReportingUser)
            .Where(report => report.Status == ReportStatus.Open)
            .OrderByDescending(report => report.CreatedAtUtc).ThenByDescending(report => report.Id)
            .ToListAsync(cancellationToken);
}
