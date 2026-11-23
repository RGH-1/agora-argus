using Agora.Web.Data.Entities;
using Agora.Web.Models.Reports;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public sealed class ReportService(IReportRepository reportRepository) : IReportService
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
        if (!CreateReportViewModel.Categories.Contains(category)) throw new ReportBusinessRuleException("Select a valid report reason.");
        var trimmedDetails = details?.Trim();
        if (trimmedDetails?.Length > CreateReportViewModel.DetailsMaximumLength) throw new ReportBusinessRuleException("Additional details cannot exceed 900 characters.");
        var reason = string.IsNullOrWhiteSpace(trimmedDetails) ? category : $"{category}: {trimmedDetails}";
        if (reason.Length > 1000) throw new ReportBusinessRuleException("Report reason cannot exceed 1000 characters.");
        var report = new Report { PostId = postId, ReportingUserId = reporterId, Reason = reason, Status = ReportStatus.Open, CreatedAtUtc = DateTime.UtcNow };
        await reportRepository.AddAsync(report, cancellationToken);
        return report;
    }

    public Task<IReadOnlyList<Report>> GetOpenAsync(CancellationToken cancellationToken = default) => reportRepository.GetOpenAsync(cancellationToken);
}

public sealed class ReportBusinessRuleException(string message) : Exception(message);
public sealed class ReportNotFoundOrForbiddenException : Exception;
