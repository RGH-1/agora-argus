using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Models.Account;

public class LoginViewModel
{
    [Required, StringLength(256), Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>The page the user wanted before being sent to the login page.</summary>
    public string? ReturnUrl { get; set; }
}