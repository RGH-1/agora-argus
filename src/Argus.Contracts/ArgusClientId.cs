using System.Text.RegularExpressions;

namespace Argus.Contracts;

/// <summary>The one rule for what a ClientId may look like, used by Argus and by the SDK.</summary>
public static partial class ArgusClientId
{
    // The ClientId becomes part of queue names and routing keys ("agora.inbox", "agora.#"),
    // where '.', '*' and '#' have a meaning, so only lowercase letters, digits and '-' are allowed.
    [GeneratedRegex("^[a-z0-9-]{1,50}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? clientId) => clientId is not null && Pattern().IsMatch(clientId);
}