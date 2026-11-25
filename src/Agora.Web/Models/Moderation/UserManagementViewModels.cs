using System.ComponentModel.DataAnnotations;

namespace Agora.Web.Models.Moderation;

public sealed class UserManagementPageViewModel
{
    public string? Search { get; init; }
    public int Page { get; init; }
    public bool HasNextPage { get; init; }
    public IReadOnlyList<ManagedUserViewModel> Users { get; init; } = [];
}

public sealed class ManagedUserViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public bool IsBanned { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public static class UserManagementActionPolicy
{
    public static bool CanChangeBan(ManagedUserViewModel target, string? actorId, bool actorIsAdmin) =>
        !target.Roles.Contains("Admin") && target.Id != actorId && (actorIsAdmin || !target.Roles.Contains("Moderator"));

    public static bool CanChangeModeratorRole(ManagedUserViewModel target, bool actorIsAdmin) =>
        actorIsAdmin && !target.Roles.Contains("Admin");
}

public sealed class UserBanFormViewModel
{
    [Required] public string UserId { get; init; } = string.Empty;
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Email { get; init; }
}
