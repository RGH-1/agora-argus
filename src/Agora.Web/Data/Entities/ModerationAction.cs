using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public enum ModerationActionType { RemovePost, DismissReport, WarnAuthor, BanAuthor }

public sealed class ModerationAction
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int PostId { get; set; }
    [Required] public string TargetAuthorId { get; set; } = string.Empty;
    [Required] public string ModeratorId { get; set; } = string.Empty;
    public ModerationActionType ActionType { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Report Report { get; set; } = null!;
    public Post Post { get; set; } = null!;
}
