using System.Text.Json.Serialization;

namespace Argus.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<InsightSeverity>))]
public enum InsightSeverity
{
    Info = 0,
    Warning,
    Critical
}