using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Moderation;

public class RejectForumViewModel
{
    public int ForumId { get; set; }
    [Required(ErrorMessage = "A rejection reason is required."), StringLength(500, ErrorMessage = "Rejection reasons cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
