using Agora.Web.Models.Moderation;

namespace Agora.Web.Services;

public interface IAiReportCheckService { Task<AiReportCheckResult> CheckAsync(int reportId, CancellationToken cancellationToken = default); }
public sealed record AiReportCheckResult(bool Succeeded, string Message, AiReportVerdict? Verdict = null);
