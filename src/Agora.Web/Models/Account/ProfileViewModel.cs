using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Account;

public class ProfileViewModel
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }
}
