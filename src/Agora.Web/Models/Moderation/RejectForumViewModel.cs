using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Moderation;

public class RejectForumViewModel
{
    public int ForumId { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}
