// 将未处理异常转换为不泄露内部细节的标准 ProblemDetails 响应（HttpApi 层）。
using System.Diagnostics;
using Axiom.News.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.News.HttpApi;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "处理请求 {TraceIdentifier} 时发生未处理异常。", httpContext.TraceIdentifier);

        // 领域不变量被破坏时返回 400，而不是掩盖成 500。
        if (exception is NewsArticleValidationException validation)
        {
            var validationDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "请求数据不符合业务规则。",
                Detail = validation.Message
            };
            validationDetails.Extensions["field"] = validation.Field;
            validationDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = validationDetails
            });
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "服务器处理请求时发生错误。"
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
