namespace Agora.Web.Services;

public interface IPublicMaintenanceNotifier
{
    Task StatusChangedAsync(MaintenanceStatus status);
}
