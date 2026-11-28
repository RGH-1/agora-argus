namespace Agora.Web.Services;

public sealed record MaintenanceStatus(bool Enabled, string? Message, DateTimeOffset? UpdatedAtUtc)
{
    public string DisplayMessage => string.IsNullOrWhiteSpace(Message)
        ? "Agora is currently under maintenance. Some actions are temporarily unavailable."
        : Message;
}

public interface IMaintenanceStateService
{
    Task<MaintenanceStatus> GetAsync(CancellationToken cancellationToken = default);
}
