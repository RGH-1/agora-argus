using Agora.Web.Data.Entities;
using Agora.Web.Services;

namespace Agora.Web.Models.Moderation;

public sealed record ArgusPanelViewModel(MaintenanceStatus Maintenance, IReadOnlyList<ArgusHealthStatusEvent> HealthEvents, IReadOnlyList<ArgusInsight> Insights)
{
    public ArgusHealthStatusEvent? LatestHealth => HealthEvents.FirstOrDefault();
}
