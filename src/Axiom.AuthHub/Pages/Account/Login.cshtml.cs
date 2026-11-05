// 为授权码流程提供基于 Identity  Cookie 的登录页面。
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.AuthHub.Pages.Account;

public sealed class LoginModel(SignInManager<IdentityUser> signInManager) : PageModel
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
            ModelState.AddModelError(string.Empty, "用户名或密码无效。");
            return Page();
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    public sealed class LoginInput
    {
        [Required]
        [StringLength(256)]
        [Display(Name = "用户名")]
        public string? UserName { get; set; }

        [Required]
        [StringLength(128)]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string? Password { get; set; }
    }
}
