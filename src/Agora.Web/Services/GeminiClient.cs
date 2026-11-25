
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Agora.Web.Services;

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default) => GenerateAsync(prompt, false, cancellationToken);
    public Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default) => GenerateAsync(prompt, true, cancellationToken);

    private async Task<string> GenerateAsync(string prompt, bool structuredJson, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Llm:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Gemini API key is missing.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_configuration["Llm:Model"] ?? "gemini-3.5-flash-lite"}:generateContent"
        );

        request.Headers.Add("x-goog-api-key", apiKey);

        request.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = structuredJson ? new { responseMimeType = "application/json", responseJsonSchema = new { type = "OBJECT", properties = new { violates = new { type = "BOOLEAN" }, ruleBroken = new { type = "STRING", nullable = true }, confidence = new { type = "NUMBER", minimum = 0, maximum = 1 }, explanation = new { type = "STRING" } }, required = new[] { "violates", "ruleBroken", "confidence", "explanation" } } } : null
        });

        using var response =
            await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseJson);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0) throw new InvalidOperationException("Gemini returned an invalid response.");
        var candidate = candidates[0];
        if (candidate.ValueKind != JsonValueKind.Object || !candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0) throw new InvalidOperationException("Gemini returned an invalid response.");
        var part = parts[0];
        if (part.ValueKind != JsonValueKind.Object || !part.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(text.GetString())) throw new InvalidOperationException("Gemini returned an invalid response.");
        return text.GetString()!;
    }
}
