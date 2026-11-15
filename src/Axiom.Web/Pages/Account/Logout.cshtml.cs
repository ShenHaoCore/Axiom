// 退出登录：清除本地 Cookie 会话；非 Mock 模式下同时发起 OIDC 单点登出。
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.Account;

public sealed class LogoutModel(
    IConfiguration configuration,
    IHostEnvironment environment) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var useMockAuth = environment.IsDevelopment()
            && configuration.GetValue("MockAuth:Enabled", false);
        if (!useMockAuth)
        {
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }

        return Redirect("~/");
    }
}
