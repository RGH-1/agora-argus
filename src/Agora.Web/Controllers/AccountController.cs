using Agora.Web.Data.Entities;
using Agora.Web.Models.Account;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Agora.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    IReportService? reportService = null) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null) => View(new RegisterViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (!await roleManager.RoleExistsAsync("Member"))
        {
            ModelState.AddModelError(string.Empty, "Registration is temporarily unavailable. Please try again later.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            DisplayName = model.DisplayName.Trim()
        };
        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Member");
        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocal(model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByNameAsync(model.UserNameOrEmail.Trim())
            ?? await userManager.FindByEmailAsync(model.UserNameOrEmail.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            return View(model);
        }
        if (user.IsBanned)
        {
            ModelState.AddModelError(string.Empty, "This account has been banned.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid username/email or password.");
            return View(model);
        }

        return RedirectToLocal(model.ReturnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home")!;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new ProfileViewModel { DisplayName = user.DisplayName, Bio = user.Bio, Warnings = reportService is null ? [] : await reportService.GetWarningsForUserAsync(user.Id) });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        user.DisplayName = model.DisplayName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        TempData["StatusMessage"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url?.IsLocalUrl(returnUrl) == true ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(HomeController.Index), "Home")!;

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }
}
