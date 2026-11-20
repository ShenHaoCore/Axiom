using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
[AllowAnonymous]
public class ErrorModel(IHostEnvironment environment) : PageModel
{
    public string? RequestId { get; private set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>UseStatusCodePagesWithReExecute 会把原始状态码以 ?code= 传入；异常重执行时为空。</summary>
    public int? ErrorCode { get; private set; }

    public bool IsNotFound => ErrorCode == StatusCodes.Status404NotFound;

    public bool ShowDevelopmentMode => environment.IsDevelopment();

    public void OnGet(int? code)
    {
        ErrorCode = code is > 0 ? code : null;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
