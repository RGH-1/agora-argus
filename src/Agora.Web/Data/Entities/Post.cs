using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public class Post
{
    public int Id { get; set; }
    public int ForumId { get; set; }
    [Required] public string AuthorId { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Content { get; set; } = string.Empty;
    public bool IsRemoved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Forum Forum { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
