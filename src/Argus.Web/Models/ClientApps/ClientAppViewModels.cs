using System.ComponentModel.DataAnnotations;
using Argus.Web.Data.Entities;

namespace Argus.Web.Models.ClientApps;

/// <summary>The "new client app" form. Thresholds start at their defaults and are set on the edit page right after.</summary>
public class NewClientAppViewModel
{
    // The same rule as ArgusClientId (shared with the SDK): it becomes part of queue names and routing keys.
    [Required, RegularExpression("^[a-z0-9-]{1,50}$", ErrorMessage = "Use 1-50 lowercase letters, digits or '-'.")]
    [Display(Name = "Client ID")]
    public string ClientId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>The client switcher in the layout.</summary>
public record ClientSwitcherViewModel(IReadOnlyList<ClientApp> Clients, ClientApp? Selected, string ReturnUrl);