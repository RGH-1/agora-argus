using Argus.Web.Data.Entities;

namespace Argus.Web.Models.Errors;

/// <summary>The error log: the latest errors of the selected client.</summary>
public class ErrorLogViewModel
{
    public required ClientApp Client { get; init; }
    public required IReadOnlyList<ErrorEvent> Errors { get; init; }

    /// <summary>How many errors the page shows at most.</summary>
    public required int Limit { get; init; }
}