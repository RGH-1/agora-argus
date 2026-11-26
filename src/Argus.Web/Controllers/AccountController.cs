using Argus.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Argus.Web.Controllers;

/// <summary>Login and logout. There is no Register: the Admin creates operator accounts (§4).</summary>
public class AccountController(SignInManager<IdentityUser> signInManager) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // lockoutOnFailure: 5 wrong passwords lock the account for 5 minutes (Identity's defaults), against password guessing.
        var result = await signInManager.PasswordSignInAsync(model.UserName.Trim(), model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            // Only local addresses: a crafted ?returnUrl=https://evil.example can't send the user elsewhere.
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");
        }

        ModelState.AddModelError(string.Empty,
            result.IsLockedOut ? "Too many failed attempts. Try again in a few minutes." : "Wrong username or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}