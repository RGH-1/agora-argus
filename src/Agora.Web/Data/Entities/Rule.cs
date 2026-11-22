using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public class Rule
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
