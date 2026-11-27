using Agora.Web.Data.Entities;
using Argus.Client;
using Microsoft.AspNetCore.Identity;

namespace Agora.Web.Services;

public sealed class RegistrationService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IArgusTracker argusTracker) : IRegistrationService
{
    public async Task<RegistrationOperationResult> RegisterAsync(string userName, string email, string displayName, string password)
    {
        if (!await roleManager.RoleExistsAsync("Member"))
            return new(null, IdentityResult.Failed(new IdentityError { Description = "Registration is temporarily unavailable. Please try again later." }));
        var user = new ApplicationUser { UserName = userName.Trim(), Email = email.Trim(), DisplayName = displayName.Trim() };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return new(null, result);
        result = await userManager.AddToRoleAsync(user, "Member");
        if (!result.Succeeded) return new(null, result);
        await ActivityTracking.TrackAsync(argusTracker, "user.registered", actorId: user.Id);
        return new(user, result);
    }
}
