namespace Argus.Web.Data;

/// <summary>Argus's two roles (§4): Admin manages operators and client apps; Operator watches health and handles incidents.</summary>
public static class ArgusRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
}