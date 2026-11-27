using Agora.Web.Data.Entities;
using Agora.Web.Models.Subscription;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

[Authorize]
public sealed class SubscriptionController(UserManager<ApplicationUser> userManager, ISubscriptionService subscriptionService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Upgrade()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Challenge() : View(new SubscriptionUpgradeViewModel { IsPro = user.IsPro, IsBanned = user.IsBanned });
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Challenge() : View(new SubscriptionUpgradeViewModel { IsPro = user.IsPro, IsBanned = user.IsBanned });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Checkout))]
    public async Task<IActionResult> ConfirmCheckout()
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var result = await subscriptionService.UpgradeToProAsync(userId);
        TempData["SubscriptionOutcome"] = result.Outcome.ToString();
        if (result.Succeeded)
        {
            TempData["SubscriptionReceiptTimeUtc"] = DateTime.UtcNow.ToString("O");
            TempData["SubscriptionReceiptReference"] = $"DEMO-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        }
        return RedirectToAction(nameof(Result));
    }

    [HttpGet]
    public async Task<IActionResult> Result()
    {
        var outcome = TempData["SubscriptionOutcome"] as string;
        if (outcome is null && (await userManager.GetUserAsync(User))?.IsPro == true)
            outcome = nameof(SubscriptionUpgradeOutcome.AlreadyPro);
        var transactionTime = DateTime.TryParse(TempData["SubscriptionReceiptTimeUtc"] as string, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTime)
            ? parsedTime : (DateTime?)null;
        var reference = TempData["SubscriptionReceiptReference"] as string;
        var model = outcome switch
        {
            nameof(SubscriptionUpgradeOutcome.Succeeded) => new SubscriptionResultViewModel { Succeeded = true, Message = "Your simulated upgrade was successful.", TransactionTimeUtc = transactionTime, Reference = reference },
            nameof(SubscriptionUpgradeOutcome.AlreadyPro) => new SubscriptionResultViewModel { AlreadyPro = true, Message = "Your Agora Pro membership is already active." },
            nameof(SubscriptionUpgradeOutcome.Banned) => new SubscriptionResultViewModel { Message = "Banned accounts cannot upgrade to Agora Pro." },
            _ => new SubscriptionResultViewModel { Message = "We could not complete the simulated checkout. Please try again." }
        };
        return View(model);
    }
}
