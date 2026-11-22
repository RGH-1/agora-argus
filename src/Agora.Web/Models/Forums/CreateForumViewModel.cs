using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Forums;

public class CreateForumViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
