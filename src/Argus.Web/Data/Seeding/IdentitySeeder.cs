using Microsoft.AspNetCore.Identity;

namespace Argus.Web.Data.Seeding;

/// <summary>
/// Creates the Admin and Operator roles and one account for each (§4). Runs on every start and does nothing once they exist.
/// Passwords come from configuration (user secrets locally, environment variables in Docker), never from code.
/// </summary>
public class IdentitySeeder(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles, IConfiguration configuration)
{
    private static readonly (string UserName, string Email, string Role)[] Accounts =
    [
        ("argus.admin", "admin@argus.local", ArgusRoles.Admin),
        ("argus.operator", "operator@argus.local", ArgusRoles.Operator),
    ];

    public async Task SeedAsync()
    {
        foreach (var role in new[] { ArgusRoles.Admin, ArgusRoles.Operator })
        {
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)), $"create the {role} role");
        }

        foreach (var (userName, email, role) in Accounts)
        {
            if (await users.FindByNameAsync(userName) is not null) continue;

            var password = configuration[$"IdentitySeed:{role}Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    $"IdentitySeed:{role}Password is missing, so '{userName}' can't be created. " +
                    "Configure it in User Secrets (locally) or as an environment variable (Docker).");

            var user = new IdentityUser { UserName = userName, Email = email, EmailConfirmed = true };
            Check(await users.CreateAsync(user, password), $"create '{userName}'");
            Check(await users.AddToRoleAsync(user, role), $"add '{userName}' to {role}");
        }
    }

    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not {what}: {string.Join(" ", result.Errors.Select(error => error.Description))}");
    }
}