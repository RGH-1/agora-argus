namespace Argus.Contracts.Tests;

public class ArgusClientIdTests
{
    [Theory]
    [InlineData("agora")]
    [InlineData("demo")]
    [InlineData("my-shop-2")]
    public void Valid_client_ids(string clientId) => Assert.True(ArgusClientId.IsValid(clientId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Agora")]     // uppercase
    [InlineData("my.shop")]   // '.' separates routing key words
    [InlineData("shop#")]     // '#' and '*' are routing key wildcards
    [InlineData("shop*")]
    [InlineData("my shop")]
    public void Invalid_client_ids(string? clientId) => Assert.False(ArgusClientId.IsValid(clientId));
}