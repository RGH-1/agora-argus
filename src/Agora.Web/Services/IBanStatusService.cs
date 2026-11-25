namespace Agora.Web.Services;

public interface IBanStatusService
{
    Task EnsureNotBannedAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class BannedUserException : Exception
{
    public BannedUserException() : base("Banned accounts cannot perform this action.") { }
}
