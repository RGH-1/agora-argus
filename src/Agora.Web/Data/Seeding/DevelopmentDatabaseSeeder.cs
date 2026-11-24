using Agora.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Data.Seeding;

/// <summary>Seeds local development data only; invoked by Program in Development.</summary>
public class DevelopmentDatabaseSeeder(
    AgoraDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration)
{
    private static readonly string[] Roles = ["Admin", "Moderator", "Member"];

    private static readonly SeedUser[] Users =
    [
        new("agora.admin", "admin@agora.local", "Admin", "Agora Administrator", "Development administrator account", false),
        new("agora.moderator", "moderator@agora.local", "Moderator", "Morgan Moderator", "Development moderator account", false),
        new("agora.member1", "member1@agora.local", "Member", "Maya Member", "Development member account", false),
        new("agora.member2", "member2@agora.local", "Member", "Priya Pro", "Development member account", true)
    ];

    private static readonly (string Title, string Content, int Order)[] Rules =
    [
        ("No harassment or hate speech", "Treat every community member with respect.", 1),
        ("No spam or misleading advertising", "Do not post repetitive, deceptive, or unsolicited promotional material.", 2),
        ("Respect other members' privacy", "Do not share personal or confidential information without permission.", 3),
        ("No illegal or harmful content", "Do not publish content that encourages illegal activity or harm.", 4)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSuccess(result, $"create role '{role}'");
            }
        }

        foreach (var seedUser in Users)
        {
            var user = await userManager.FindByNameAsync(seedUser.UserName);
            if (user is null)
            {
                var password = configuration[$"DevelopmentSeed:{seedUser.Role}Password"];
                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException(
                        $"Development seed password 'DevelopmentSeed:{seedUser.Role}Password' is missing. " +
                        "Configure it in Agora.Web User Secrets; development accounts were not created.");
                }

                user = new ApplicationUser
                {
                    UserName = seedUser.UserName,
                    Email = seedUser.Email,
                    EmailConfirmed = true,
                    DisplayName = seedUser.DisplayName,
                    Bio = seedUser.Bio,
                    IsPro = seedUser.IsPro,
                    IsBanned = false
                };
                EnsureSuccess(await userManager.CreateAsync(user, password), $"create user '{seedUser.UserName}'");
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
            {
                EnsureSuccess(await userManager.AddToRoleAsync(user, seedUser.Role),
                    $"assign role '{seedUser.Role}' to '{seedUser.UserName}'");
            }
        }

        if (await dbContext.RuleSeedStates.FindAsync([1], cancellationToken) is null)
        {
            foreach (var seedRule in Rules)
            {
                dbContext.Rules.Add(new Rule
                {
                    Title = seedRule.Title,
                    Content = seedRule.Content,
                    DisplayOrder = seedRule.Order,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            dbContext.RuleSeedStates.Add(new RuleSeedState { Id = 1, InitialRulesSeededAtUtc = DateTime.UtcNow });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSuccess(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to {action}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    private sealed record SeedUser(string UserName, string Email, string Role, string DisplayName, string Bio, bool IsPro);
}
