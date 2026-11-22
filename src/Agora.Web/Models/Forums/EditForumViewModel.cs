using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Forums;

public class EditForumViewModel
{
    public int Id { get; set; }
    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
