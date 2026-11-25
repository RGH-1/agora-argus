using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Data.Entities;

public sealed class UserBanAction
{
    public int Id { get; set; }
    [Required] public string ActorId { get; set; } = string.Empty;
    [Required] public string TargetUserId { get; set; } = string.Empty;
    public bool IsBan { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
