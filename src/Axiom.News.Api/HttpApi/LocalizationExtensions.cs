// 把校验错误码批量转换为当前语言的消息文本（HttpApi 层内部使用）。
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Resources;
using Microsoft.Extensions.Localization;

namespace Axiom.News.HttpApi;

internal static class LocalizationExtensions
{
    public static Dictionary<string, string[]> ToMessages(
        this Dictionary<string, ValidationError[]> errors,
        IStringLocalizer<ApiErrorMessages> localizer) =>
        errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                .Select(error => localizer[error.ErrorCode, error.Args].Value)
                .ToArray());
}
