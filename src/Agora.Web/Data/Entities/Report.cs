using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public class Report
{
    public int Id { get; set; }
    public int PostId { get; set; }
    [Required] public string ReportingUserId { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    [StringLength(4000)] public string? AiAssessment { get; set; }
    public decimal? AiConfidence { get; set; }
    public DateTime? AiAssessedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Post Post { get; set; } = null!;
    public ApplicationUser ReportingUser { get; set; } = null!;
}
