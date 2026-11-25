namespace Agora.Web.Models.Moderation;

public sealed class AiReportVerdict
{
    public bool Violates { get; init; }
    public string? RuleBroken { get; init; }
    public decimal Confidence { get; init; }
    public string Explanation { get; init; } = string.Empty;
}
