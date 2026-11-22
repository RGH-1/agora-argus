
using Agora.Web.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Xunit;

namespace Agora.Web.Tests;

public class GeminiClientTests
{
    // Creates a Gemini client with a fake API key
    // and a fake HTTP handler for unit testing.
    private static GeminiClient CreateClient(
        HttpMessageHandler handler,
        string? apiKey = "fake-test-key")
    {
        var settings = new Dictionary<string, string?>
        {
            ["Llm:ApiKey"] = apiKey
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new GeminiClient(
            new HttpClient(handler), config);
    }

    // TEST 1: Successful API response
    [Fact]
    public async Task ValidResponse_ReturnsGeminiText()
    {
        var handler = new FakeHandler();
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(
            "Reply only with GEMINI_OK");

        Assert.Equal("GEMINI_OK", result);
        Assert.Equal("fake-test-key", handler.SentApiKey);
        Assert.Contains("GEMINI_OK", handler.SentBody);
        Assert.Contains(
            "gemini-3.5-flash-lite",
            handler.SentUrl);
    }

    // TEST 2: Missing API key
    [Fact]
    public async Task MissingApiKey_ThrowsException()
    {
        var handler = new FakeHandler();
        var client = CreateClient(handler, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("Hello"));

        Assert.False(handler.WasCalled);
    }

    // TEST 3: API returns an error
    [Fact]
    public async Task ApiFailure_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler
        {
            Status = HttpStatusCode.ServiceUnavailable
        };

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("Hello"));
    }

    // TEST 4: Real Gemini API request
    // Disabled unless RUN_GEMINI_LIVE_TEST=1.
    [LiveGeminiFact]
    [Trait("Category", "Live")]
    public async Task GeminiApi_RealRequest_ReturnsExpectedText()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(GeminiClient).Assembly,
                optional: true)
            .Build();

        Assert.False(
            string.IsNullOrWhiteSpace(config["Llm:ApiKey"]),
            "Gemini API key is missing from User Secrets.");

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        var client = new GeminiClient(
            httpClient, config);

        var result = await client.GenerateAsync(
            "Reply with only GEMINI_OK.");

        Assert.Contains(
            "GEMINI_OK",
            result.ToUpperInvariant());
    }

    // Automatically skip the live test unless enabled.
    public sealed class LiveGeminiFactAttribute : FactAttribute
    {
        public LiveGeminiFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(
                    "RUN_GEMINI_LIVE_TEST") != "1")
            {
                Skip = "Live Gemini test disabled.";
            }
        }
    }

    // Fake HTTP handler used by the unit tests
    private class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; }
            = HttpStatusCode.OK;

        public bool WasCalled { get; private set; }
        public string? SentApiKey { get; private set; }
        public string? SentBody { get; private set; }
        public string? SentUrl { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            WasCalled = true;

            SentUrl = request.RequestUri?.ToString();

            if (request.Headers.TryGetValues(
                "x-goog-api-key", out var values))
            {
                SentApiKey = values.FirstOrDefault();
            }

            SentBody = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            var json = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "GEMINI_OK" }
                    ]
                  }
                }
              ]
            }
            """;

            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
