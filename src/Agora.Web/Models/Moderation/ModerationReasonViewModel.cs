using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Moderation;

public sealed class ModerationReasonViewModel
{
    public int ReportId { get; set; }
    [Required, StringLength(500), Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;
}
