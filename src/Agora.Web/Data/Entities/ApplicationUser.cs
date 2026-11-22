using Microsoft.AspNetCore.Identity;

namespace Agora.Web.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool IsPro { get; set; }
    public bool IsBanned { get; set; }

    public ICollection<Forum> CreatedForums { get; set; } = new List<Forum>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
