using System.ComponentModel.DataAnnotations;
using Agora.Web.Data.Entities;

namespace Agora.Web.Models.Account;

public class ProfileViewModel
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }
    public bool IsPro { get; set; }
    public IReadOnlyList<UserWarning> Warnings { get; set; } = [];
}
