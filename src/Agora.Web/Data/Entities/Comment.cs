using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    [Required] public string AuthorId { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Post Post { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
}
