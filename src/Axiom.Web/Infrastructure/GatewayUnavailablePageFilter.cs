// 全局 Razor Pages 过滤器：把网关不可用（传输层）异常转成页面内可呈现的错误。
// POST：保留表单（PageResult + ModelState 错误）；GET：跳转错误页。
using Axiom.Web.Clients;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Infrastructure;

public sealed class GatewayUnavailablePageFilter(IStringLocalizer<SharedResource> localizer)
    : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not GatewayUnavailableException)
        {
            return;
        }

        executed.ExceptionHandled = true;
        var isPost = context.HandlerMethod?.MethodInfo.Name.StartsWith("OnPost", StringComparison.Ordinal) == true;
        if (isPost && context.HandlerInstance is PageModel pageModel)
        {
            // 写操作：回到当前页并给内联错误，表单值留在 ModelState/页面模型中不丢。
            pageModel.ModelState.AddModelError(string.Empty, localizer["Common.GatewayUnavailable"].Value);
            executed.Result = new PageResult();
            return;
        }

        // 读操作：交给统一错误页（带状态码文案）。
        executed.Result = new RedirectResult("/Error?code=503");
    }
}
