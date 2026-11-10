// 为授权码流程提供基于 Identity  Cookie 的登录页面。
using System.ComponentModel.DataAnnotations;
using Axiom.AuthHub.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.AuthHub.Pages.Account;

public sealed class LoginModel(
    SignInManager<IdentityUser> signInManager,
    IStringLocalizer<AuthResource> localizer) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            Input.UserName!,
            Input.Password!,
            isPersistent: false,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, localizer["Login.InvalidCredentials"]);
            return Page();
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Validation.UserNameRequired")]
        [StringLength(256, ErrorMessage = "Validation.UserNameTooLong")]
        [Display(Name = "Field.UserName")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "Validation.PasswordRequired")]
        [StringLength(128, ErrorMessage = "Validation.PasswordTooLong")]
        [DataType(DataType.Password)]
        [Display(Name = "Field.Password")]
        public string? Password { get; set; }
    }
}
