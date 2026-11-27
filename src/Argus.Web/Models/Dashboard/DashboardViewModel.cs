using Argus.Web.Data.Entities;
using Argus.Web.Services;

namespace Argus.Web.Models.Dashboard;

/// <summary>The dashboard: every chart of the selected client for the selected time range.</summary>
public class DashboardViewModel
{
    public required IReadOnlyList<ClientApp> Clients { get; init; }
    public required ClientApp Client { get; init; }

    /// <summary>"hour", "day" or "week".</summary>
    public required string Range { get; init; }
    public required IReadOnlyList<ChartData> Charts { get; init; }
}