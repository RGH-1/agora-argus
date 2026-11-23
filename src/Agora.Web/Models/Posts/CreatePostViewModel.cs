using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Posts;
public class CreatePostViewModel
{
    public int ForumId { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Content { get; set; } = string.Empty;
}
