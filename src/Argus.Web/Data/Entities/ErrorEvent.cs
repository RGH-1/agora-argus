using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Data.Entities;

/// <summary>An unhandled exception a client reported with ErrorReported.</summary>
public class ErrorEvent
{
    public long Id { get; set; }
    public int ClientAppId { get; set; }
    public ClientApp ClientApp { get; set; } = null!;
    [Required, StringLength(2000)] public string Message { get; set; } = string.Empty;
    [Required, StringLength(300)] public string ExceptionType { get; set; } = string.Empty;
    [StringLength(500)] public string? Path { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}