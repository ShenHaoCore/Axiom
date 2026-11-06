// 将未处理异常转换为不泄露内部细节的标准 ProblemDetails 响应（HttpApi 层）。
using System.Diagnostics;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Resources;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Axiom.ReferenceData.HttpApi;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService,
    IStringLocalizer<ApiErrorMessages> localizer) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "处理请求 {TraceIdentifier} 时发生未处理异常。", httpContext.TraceIdentifier);

        // 领域不变量被破坏时返回 400，而不是掩盖成 500。
        var validation = exception switch
        {
            Domain.Exceptions.RegionValidationException region => (region.Field, region.ErrorCode, region.Args),
            Domain.Exceptions.ReferenceLocationValidationException location => (location.Field, location.ErrorCode, location.Args),
            _ => (null, null, Array.Empty<object>())
        };

        if (validation.ErrorCode is not null)
        {
            var validationDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = localizer[ApiErrorCodes.VALIDATION_FAILED].Value,
                Detail = localizer[validation.ErrorCode, validation.Args].Value
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
            Title = localizer[ApiErrorCodes.SERVER_INTERNAL_ERROR].Value
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
