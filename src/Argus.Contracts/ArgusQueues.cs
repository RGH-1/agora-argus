namespace Argus.Contracts;

/// <summary>Queue names (design draft §7.1). The topology that declares them comes in AA-6.</summary>
public static class ArgusQueues
{
    public const string Health = "argus.health";
    public const string Analytics = "argus.analytics";
    public const string Dead = "argus.dead";

    public static string Inbox(string clientId) => $"{clientId}.inbox";
    public static string ClientDead(string clientId) => $"{clientId}.dead";
}