namespace Argus.Contracts.Health;

/// <summary>Client → Argus, routing key health.error. One unhandled exception in the client.</summary>
public class ErrorReported : ArgusMessage
{
    public string Message { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>The request path, when the error happened during a request.</summary>
    public string? Path { get; set; }
}