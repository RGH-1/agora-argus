using Argus.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace Argus.Web.Services;

/// <summary>An Argus staff account as the operators page shows it.</summary>
public record StaffAccount(string Id, string UserName, string Role);

public interface IOperatorService
{
    /// <summary>Every Admin and Operator account, by name.</summary>
    Task<IReadOnlyList<StaffAccount>> GetAllAsync();

    /// <summary>Creates an Operator account. Returns Identity's errors, e.g. a weak password or a name that's taken.</summary>
    Task<IdentityResult> CreateOperatorAsync(string userName, string password);

    /// <summary>Deletes an Operator account. Admins can't be deleted here. False when the id isn't an Operator.</summary>
    Task<bool> DeleteOperatorAsync(string id);
}

public class OperatorService(UserManager<IdentityUser> users) : IOperatorService
{
    public async Task<IReadOnlyList<StaffAccount>> GetAllAsync()
    {
        var admins = await users.GetUsersInRoleAsync(ArgusRoles.Admin);
        var operators = await users.GetUsersInRoleAsync(ArgusRoles.Operator);
        return admins.Select(user => new StaffAccount(user.Id, user.UserName!, ArgusRoles.Admin))
            .Concat(operators.Select(user => new StaffAccount(user.Id, user.UserName!, ArgusRoles.Operator)))
            .OrderBy(account => account.UserName)
            .ToList();
    }

    public async Task<IdentityResult> CreateOperatorAsync(string userName, string password)
    {
        var user = new IdentityUser { UserName = userName.Trim() };
        var created = await users.CreateAsync(user, password); // Identity checks the password rules and unique names
        return created.Succeeded ? await users.AddToRoleAsync(user, ArgusRoles.Operator) : created;
    }

    public async Task<bool> DeleteOperatorAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        // Only operators: an Admin (including the one clicking) can never be removed from this page.
        if (user is null || await users.IsInRoleAsync(user, ArgusRoles.Admin) || !await users.IsInRoleAsync(user, ArgusRoles.Operator))
            return false;

        return (await users.DeleteAsync(user)).Succeeded;
    }
}