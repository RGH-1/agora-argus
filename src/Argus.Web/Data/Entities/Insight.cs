using System.ComponentModel.DataAnnotations;
using Argus.Contracts;

namespace Argus.Web.Data.Entities;

/// <summary>An AI suggestion for a client (§4.4). Advisory only.</summary>
public class Insight
{
    public int Id { get; set; }
    public int ClientAppId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Detail { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ClientApp ClientApp { get; set; } = null!;
}