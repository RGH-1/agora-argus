using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Reports;

public sealed class CreateReportViewModel : IValidatableObject
{
    public const int DetailsMaximumLength = 900;
    public int PostId { get; set; }

    [Required(ErrorMessage = "Select a report reason.")]
    public string Category { get; set; } = string.Empty;

    [StringLength(DetailsMaximumLength, ErrorMessage = "Additional details cannot exceed 900 characters.")]
    public string? Details { get; set; }

    public static IReadOnlyList<string> Categories { get; } =
    ["Spam or misleading content", "Harassment or bullying", "Hate speech", "Threats or violence", "Inappropriate content", "Other"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Categories.Contains(Category))
            yield return new ValidationResult("Select a valid report reason.", [nameof(Category)]);
    }
}
