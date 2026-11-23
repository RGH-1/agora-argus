using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public class Forum
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(100)] public string NormalizedName { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(500)] public string? RejectionReason { get; set; }
    public ForumStatus Status { get; set; } = ForumStatus.Pending;
    [Required] public string CreatorId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ApplicationUser Creator { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
