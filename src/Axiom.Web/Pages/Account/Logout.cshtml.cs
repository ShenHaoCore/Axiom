// 退出登录：清除本地 Cookie 会话；非 Mock 模式下同时发起 OIDC 单点登出。
// 必须以 SignOut 结果返回：OIDC handler 会把响应重定向到 IdP 的 end_session 端点。
// 旧实现先 await SignOutAsync 再 Redirect("~/")，SSO 端会话重定向被覆盖，登出形同虚设。
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
    public IActionResult OnPostAsync()
    {
        var useMockAuth = environment.IsDevelopment()
            && configuration.GetValue("MockAuth:Enabled", false);
        return useMockAuth
            ? SignOut(CookieAuthenticationDefaults.AuthenticationScheme)
            : SignOut(
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
    }
}
