using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Comments;

public sealed class CreateCommentViewModel : IValidatableObject
{
    public int PostId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Content))
            yield return new ValidationResult("Comment content is required.", [nameof(Content)]);
    }
}
