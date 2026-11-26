using System.ComponentModel.DataAnnotations;
using Argus.Web.Services;

namespace Argus.Web.Models.Operators;

/// <summary>The operators page: the staff list plus the "new operator" form.</summary>
public class OperatorsViewModel
{
    public IReadOnlyList<StaffAccount> Accounts { get; set; } = [];
    public NewOperatorViewModel NewOperator { get; set; } = new();
}

public class NewOperatorViewModel
{
    [Required, StringLength(50), RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, digits, '.', '_' or '-'.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    // Identity also checks its own rules (a digit, upper and lower case, a symbol).
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}