// 校验写操作的 CSRF 令牌，失败时返回 JSON 错误码。
using Microsoft.AspNetCore.Antiforgery;

namespace Axiom.Bff.Endpoints;

public sealed class RequireAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Json(new { code = "csrf_failed" }, statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
