using Agora.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Agora.Web.Services;

public interface IRegistrationService
{
    Task<RegistrationOperationResult> RegisterAsync(string userName, string email, string displayName, string password);
}

public sealed record RegistrationOperationResult(ApplicationUser? User, IdentityResult Result);
