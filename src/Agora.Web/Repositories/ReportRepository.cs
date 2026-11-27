using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Moderation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class ReportRepository(AgoraDbContext dbContext) : IReportRepository
{
    public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts.AsNoTracking().Include(post => post.Forum).FirstOrDefaultAsync(post =>
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
        await dbContext.Reports.AsNoTracking().Include(report => report.Post).ThenInclude(post => post.Author).Include(report => report.ReportingUser)
            .Where(report => report.Status == ReportStatus.Open)
            .OrderBy(report => report.CreatedAtUtc).ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

    public Task<Report?> GetOpenForModerationAsync(int reportId, CancellationToken cancellationToken = default) =>
        dbContext.Reports.AsNoTracking().Include(report => report.Post).ThenInclude(post => post.Author).Include(report => report.Post.Forum)
            .Include(report => report.ReportingUser).FirstOrDefaultAsync(report => report.Id == reportId, cancellationToken);

    public Task<Report?> GetOpenForAiCheckAsync(int reportId, CancellationToken cancellationToken = default) =>
        dbContext.Reports.AsNoTracking().Include(report => report.Post).FirstOrDefaultAsync(report => report.Id == reportId && report.Status == ReportStatus.Open, cancellationToken);

    public async Task<bool> TrySaveAiAssessmentAsync(int reportId, string assessment, decimal confidence, DateTime assessedAtUtc, CancellationToken cancellationToken = default) =>
        await dbContext.Reports.Where(report => report.Id == reportId && report.Status == ReportStatus.Open)
            .ExecuteUpdateAsync(setters => setters.SetProperty(report => report.AiAssessment, assessment).SetProperty(report => report.AiConfidence, confidence).SetProperty(report => report.AiAssessedAtUtc, assessedAtUtc), cancellationToken) == 1;

    public async Task<ModerationResolutionResult> ResolveAsync(int reportId, string moderatorId, ModerationActionType actionType, string? reason, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var report = await dbContext.Reports.Include(item => item.Post).ThenInclude(post => post.Author)
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return ModerationResolutionResult.NotFound;
        var relatedReports = actionType == ModerationActionType.RemovePost
            ? await dbContext.Reports.AsNoTracking().Where(item => item.PostId == report.PostId && item.Status == ReportStatus.Open).ToListAsync(cancellationToken)
            : [report];

        var resolved = await dbContext.Reports.Where(item => item.Id == reportId && item.Status == ReportStatus.Open)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, actionType == ModerationActionType.DismissReport ? ReportStatus.Dismissed : ReportStatus.Reviewed), cancellationToken);
        if (resolved == 0) return ModerationResolutionResult.AlreadyHandled;

        var now = DateTime.UtcNow;
        if (actionType == ModerationActionType.RemovePost)
        {
            report.Post.IsRemoved = true;
            await dbContext.Reports.Where(item => item.PostId == report.PostId && item.Id != reportId && item.Status == ReportStatus.Open)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, ReportStatus.Reviewed), cancellationToken);
        }
        else if (actionType == ModerationActionType.BanAuthor)
        {
            if (report.Post.Author.IsBanned) return ModerationResolutionResult.AlreadyBanned;
            report.Post.Author.IsBanned = true;
        }
        else if (actionType == ModerationActionType.WarnAuthor)
        {
            dbContext.UserWarnings.Add(new UserWarning { UserId = report.Post.AuthorId, ReportId = report.Id, ModeratorId = moderatorId, Message = reason!, CreatedAtUtc = now });
        }

        foreach (var related in relatedReports)
            dbContext.ModerationActions.Add(new ModerationAction { ReportId = related.Id, PostId = report.PostId, TargetAuthorId = report.Post.AuthorId, ModeratorId = moderatorId, ActionType = actionType, Reason = related.Id == reportId ? reason : "Post removed while resolving a related report.", CreatedAtUtc = now });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return ModerationResolutionResult.Resolved;
    }

    public async Task<IReadOnlyList<ModerationHistoryItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default) =>
        await (from action in dbContext.ModerationActions.AsNoTracking()
               join moderator in dbContext.Users.AsNoTracking() on action.ModeratorId equals moderator.Id into moderators
               from moderator in moderators.DefaultIfEmpty()
               orderby action.CreatedAtUtc descending, action.Id descending
               select new ModerationHistoryItemViewModel
               {
                   ReportId = action.ReportId,
                   PostId = action.PostId,
                   ModeratorDisplayName = moderator != null && !string.IsNullOrWhiteSpace(moderator.DisplayName)
                       ? moderator.DisplayName
                       : moderator != null && !string.IsNullOrWhiteSpace(moderator.Email) ? moderator.Email : action.ModeratorId,
                   ActionType = action.ActionType,
                   Reason = action.Reason,
                   CreatedAtUtc = action.CreatedAtUtc
               }).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserWarning>> GetWarningsForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserWarnings.AsNoTracking().Where(warning => warning.UserId == userId)
            .OrderByDescending(warning => warning.CreatedAtUtc).ThenByDescending(warning => warning.Id).ToListAsync(cancellationToken);

    private static bool IsOpenReportUniquenessViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 }
        || exception.InnerException?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
           && exception.InnerException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
}
