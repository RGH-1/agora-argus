using Argus.Web.Data.Entities;

namespace Argus.Web.Controllers;

/// <summary>
/// Which client the per-client pages show (§4.1: "all views are per client").
/// A ?clientId= in the address wins and is remembered in a cookie; otherwise the cookie; otherwise the first client.
/// </summary>
public static class ClientSelection
{
    public const string CookieName = "argus.client";

    /// <summary>Null when ?clientId= names a client that doesn't exist (the page returns 404), or when there are no clients.</summary>
    public static ClientApp? Resolve(HttpContext http, IReadOnlyList<ClientApp> clients, string? clientId)
    {
        if (clientId is not null)
        {
            var requested = clients.FirstOrDefault(app => app.ClientId == clientId);
            if (requested is not null) Remember(http, requested.ClientId);
            return requested;
        }

        // A remembered client that was deleted since then falls back to the first one.
        return clients.FirstOrDefault(app => app.ClientId == http.Request.Cookies[CookieName]) ?? clients.FirstOrDefault();
    }

    public static void Remember(HttpContext http, string clientId) =>
        http.Response.Cookies.Append(CookieName, clientId, new CookieOptions
        {
            HttpOnly = true, // only the server reads it
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(365),
        });
}