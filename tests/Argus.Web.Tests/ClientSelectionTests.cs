using Argus.Web.Controllers;
using Argus.Web.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace Argus.Web.Tests;

/// <summary>The client switcher's rules: ?clientId= wins and is remembered, then the cookie, then the first client.</summary>
public class ClientSelectionTests
{
    private static readonly ClientApp[] Clients = [new() { Id = 1, ClientId = "agora" }, new() { Id = 2, ClientId = "demo" }];

    private static DefaultHttpContext Request(string? cookie = null)
    {
        var http = new DefaultHttpContext();
        if (cookie is not null) http.Request.Headers.Cookie = $"{ClientSelection.CookieName}={cookie}";
        return http;
    }

    [Fact]
    public void ClientId_in_the_address_wins_and_is_remembered()
    {
        var http = Request(cookie: "agora");

        Assert.Equal("demo", ClientSelection.Resolve(http, Clients, "demo")?.ClientId);
        Assert.Contains($"{ClientSelection.CookieName}=demo", http.Response.Headers.SetCookie.ToString());
    }

    [Theory]
    [InlineData("demo", "demo")]   // the remembered choice
    [InlineData(null, "agora")]    // nothing chosen yet: the first client
    [InlineData("deleted", "agora")] // the remembered client no longer exists
    public void Without_a_clientId_the_cookie_or_the_first_client_is_used(string? cookie, string expected)
    {
        var http = Request(cookie);

        Assert.Equal(expected, ClientSelection.Resolve(http, Clients, null)?.ClientId);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count); // nothing new to remember
    }

    [Fact]
    public void An_unknown_clientId_in_the_address_is_not_found()
    {
        var http = Request(cookie: "agora");

        Assert.Null(ClientSelection.Resolve(http, Clients, "ghost"));
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }
}