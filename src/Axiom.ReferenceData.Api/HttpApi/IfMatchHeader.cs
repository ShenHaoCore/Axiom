// If-Match 解析（HttpApi 层）：地点档案与行政区共用同一套 428/400 语义。
// 放在这里而不是各自端点内，避免两个资源各自维护一份解析逻辑而漂移。
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Resources;
using Microsoft.Extensions.Localization;

namespace Axiom.ReferenceData.HttpApi;

public static class IfMatchHeader
{
    /// <summary>
    /// 解析 If-Match 中的 ETag（形如 "32位GUID"）。
    /// 缺失返回 428，格式错误返回 400；解析成功时输出并发令牌。
    /// </summary>
    public static bool TryParse(
        string etag,
        IStringLocalizer<ApiErrorMessages> localizer,
        out Guid expectedToken,
        out IResult? error)
    {
        expectedToken = default;
        error = null;

        if (string.IsNullOrWhiteSpace(etag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: localizer[ApiErrorCodes.REQUEST_MISSING_IF_MATCH].Value);
            return false;
        }

        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' ||
            !Guid.TryParseExact(etag[1..^1], "N", out expectedToken))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer[ApiErrorCodes.REQUEST_INVALID_IF_MATCH].Value);
            return false;
        }

        return true;
    }
}
