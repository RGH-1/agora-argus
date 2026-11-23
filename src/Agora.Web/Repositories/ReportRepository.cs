using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class ReportRepository(AgoraDbContext dbContext) : IReportRepository
{
    public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().FirstOrDefaultAsync(post =>
            post.Id == postId && !post.IsRemoved && post.Forum.Status == ForumStatus.Approved, cancellationToken);

    public Task<bool> HasOpenReportAsync(int postId, string reportingUserId, CancellationToken cancellationToken = default) =>
        dbContext.Reports.AnyAsync(report => report.PostId == postId && report.ReportingUserId == reportingUserId && report.Status == ReportStatus.Open, cancellationToken);

    public async Task<ReportAddResult> AddAsync(Report report, CancellationToken cancellationToken = default)
    {
        await dbContext.Reports.AddAsync(report, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ReportAddResult.Added;
        }
        catch (DbUpdateException exception) when (IsOpenReportUniquenessViolation(exception))
        {
            dbContext.Entry(report).State = EntityState.Detached;
            return ReportAddResult.DuplicateOpenReport;
        }
    }

    public async Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Reports.AsNoTracking().Include(report => report.Post).Include(report => report.ReportingUser)
            .Where(report => report.Status == ReportStatus.Open)
            .OrderByDescending(report => report.CreatedAtUtc).ThenByDescending(report => report.Id)
            .ToListAsync(cancellationToken);

    private static bool IsOpenReportUniquenessViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
           && exception.InnerException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
}
