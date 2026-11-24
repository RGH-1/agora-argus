using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public sealed class UserWarning
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public int ReportId { get; set; }
    [Required] public string ModeratorId { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Report Report { get; set; } = null!;
}
