using MinGo.IdentityKit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Samples.EmbeddedServerRendering;

public sealed class IndexModel(
    SignInManager<IdentityUser> signIn,
    PublicRegistration<IdentityUser> registration,
    IOptionsMonitor<IdentityKitOptions> options) : PageModel
{
    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public bool RegistrationEnabled => options.CurrentValue.Registration.Enabled;

    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (ModelState.IsValid && (await signIn.PasswordSignInAsync(
            UserName, Password, isPersistent: false, lockoutOnFailure: true)).Succeeded)
            return RedirectToPage();

        ModelState.AddModelError("", "Unable to sign in.");
        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await registration.RegisterAsync(UserName, Password);
        if (result.Succeeded)
            return RedirectToPage();

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage();
    }
}
