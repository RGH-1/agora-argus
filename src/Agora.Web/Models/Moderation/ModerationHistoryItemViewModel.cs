using Agora.Web.Data.Entities;

namespace Agora.Web.Models.Moderation;

public sealed class ModerationHistoryItemViewModel
{
    public int ReportId { get; init; }
    public int PostId { get; init; }
    public string ModeratorDisplayName { get; init; } = string.Empty;
    public ModerationActionType ActionType { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public string ActionLabel => ActionType switch
    {
        ModerationActionType.DismissReport => "Dismiss report",
        ModerationActionType.RemovePost => "Remove post",
        ModerationActionType.WarnAuthor => "Warn author",
        ModerationActionType.BanAuthor => "Ban author",
        _ => ActionType.ToString()
    };
}
