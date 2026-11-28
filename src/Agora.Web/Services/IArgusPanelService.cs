using Agora.Web.Models.Moderation;

namespace Agora.Web.Services;

public interface IArgusPanelService { Task<ArgusPanelViewModel> GetAsync(CancellationToken cancellationToken = default); }
