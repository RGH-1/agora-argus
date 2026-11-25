namespace Agora.Web.Services;

public interface IGeminiClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);
}
