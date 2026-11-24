using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Rules;

public sealed class RuleFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "A rule title is required."), StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "Rule content is required."), StringLength(2000)] public string Content { get; set; } = string.Empty;
}
