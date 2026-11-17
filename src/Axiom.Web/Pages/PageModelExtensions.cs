// 页面通用结果映射：把网关返回的 401/403 统一转成登录挑战或禁止访问。
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages;

public static class PageModelExtensions
{
    public static IActionResult? HandleAuthStatus(this PageModel page, HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => page.Challenge(),
            HttpStatusCode.Forbidden => page.StatusCode(StatusCodes.Status403Forbidden),
            _ => null
        };
}
