using Agora.Web.Data.Entities;
using Agora.Web.Models.Reports;
using Agora.Web.Models.Moderation;
using Agora.Web.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Agora.Web.Services;

public sealed class ReportService(IReportRepository reportRepository, UserManager<ApplicationUser>? userManager = null) : IReportService
{
    public async Task<Post> GetReportablePostAsync(int postId, string reporterId, CancellationToken cancellationToken = default)
    {
        var post = await reportRepository.GetPublicPostAsync(postId, cancellationToken) ?? throw new ReportNotFoundOrForbiddenException();
        if (post.AuthorId == reporterId) throw new ReportBusinessRuleException("You cannot report your own post.");
        return post;
    }

    public async Task<Report> CreateAsync(int postId, string category, string? details, string reporterId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reporterId)) throw new ArgumentException("Reporter ID is required.", nameof(reporterId));
        await GetReportablePostAsync(postId, reporterId, cancellationToken);
        if (await reportRepository.HasOpenReportAsync(postId, reporterId, cancellationToken)) throw new OpenReportAlreadyExistsException();
        if (!CreateReportViewModel.Categories.Contains(category)) throw new ReportBusinessRuleException("Select a valid report reason.");
        var trimmedDetails = details?.Trim();
        if (trimmedDetails?.Length > CreateReportViewModel.DetailsMaximumLength) throw new ReportBusinessRuleException("Additional details cannot exceed 900 characters.");
        var reason = string.IsNullOrWhiteSpace(trimmedDetails) ? category : $"{category}: {trimmedDetails}";
        if (reason.Length > 1000) throw new ReportBusinessRuleException("Report reason cannot exceed 1000 characters.");
        var report = new Report { PostId = postId, ReportingUserId = reporterId, Reason = reason, Status = ReportStatus.Open, CreatedAtUtc = DateTime.UtcNow };
        if (await reportRepository.AddAsync(report, cancellationToken) == ReportAddResult.DuplicateOpenReport) throw new OpenReportAlreadyExistsException();
        return report;
    }

    public Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default) => reportRepository.GetOpenAsync(cancellationToken);
    public Task<IReadOnlyList<ModerationHistoryItemViewModel>> GetHistoryAsync(CancellationToken cancellationToken = default) => reportRepository.GetHistoryAsync(cancellationToken);
    public Task<IReadOnlyList<UserWarning>> GetWarningsForUserAsync(string userId, CancellationToken cancellationToken = default) => reportRepository.GetWarningsForUserAsync(userId, cancellationToken);

    public async Task ResolveAsync(int reportId, string moderatorId, ModerationActionType actionType, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moderatorId)) throw new ArgumentException("Moderator ID is required.", nameof(moderatorId));
        if (actionType == ModerationActionType.WarnAuthor && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500))
            throw new ReportBusinessRuleException("A warning message of up to 500 characters is required.");
        if (!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length > 500)
            throw new ReportBusinessRuleException("The moderation reason cannot exceed 500 characters.");

        var report = await reportRepository.GetOpenForModerationAsync(reportId, cancellationToken) ?? throw new ReportNotFoundOrForbiddenException();
        if (report.Status != ReportStatus.Open) throw new ReportAlreadyHandledException();
        if (report.Post.AuthorId == moderatorId) throw new ReportBusinessRuleException("You cannot moderate your own post.");
        await EnsureTargetCanBeModeratedAsync(report.Post.AuthorId, moderatorId);

        var result = await reportRepository.ResolveAsync(reportId, moderatorId, actionType, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), cancellationToken);
        if (result == ModerationResolutionResult.AlreadyHandled) throw new ReportAlreadyHandledException();
        if (result == ModerationResolutionResult.AlreadyBanned) throw new ReportBusinessRuleException("This author is already banned.");
        if (result == ModerationResolutionResult.NotFound) throw new ReportNotFoundOrForbiddenException();
    }

    private async Task EnsureTargetCanBeModeratedAsync(string targetUserId, string moderatorId)
    {
        if (userManager is null) return;
        var target = await userManager.FindByIdAsync(targetUserId) ?? throw new ReportNotFoundOrForbiddenException();
        if (await userManager.IsInRoleAsync(target, "Admin")) throw new ReportBusinessRuleException("Admin accounts cannot be moderated here.");
        if (await userManager.IsInRoleAsync(target, "Moderator"))
        {
            var moderator = await userManager.FindByIdAsync(moderatorId) ?? throw new ReportNotFoundOrForbiddenException();
            if (!await userManager.IsInRoleAsync(moderator, "Admin")) throw new ReportBusinessRuleException("Only an Admin can moderate a Moderator account.");
        }
    }
}

public sealed class ReportBusinessRuleException(string message) : Exception(message);
public sealed class ReportNotFoundOrForbiddenException : Exception;
public sealed class OpenReportAlreadyExistsException : Exception
{
    public const string FriendlyMessage = "You have already reported this post. Our moderation team will review it.";
    public OpenReportAlreadyExistsException() : base(FriendlyMessage) { }
}
public sealed class ReportAlreadyHandledException : Exception
{
    public const string FriendlyMessage = "Another moderator has already handled this report.";
    public ReportAlreadyHandledException() : base(FriendlyMessage) { }
}
