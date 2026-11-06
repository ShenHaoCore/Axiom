// 定义地点列表共享的筛选、分页参数及输入边界。
// 校验只产出错误码与参数，由接口层按当前语言渲染消息。
using Axiom.SharedKernel.Errors;

namespace Axiom.Contracts.ReferenceData;

public sealed record LocationQueryParameters(
    string? Code = null,
    string? Name = null,
    int Page = 1,
    int PageSize = 20)
{
    public const int MAX_PAGE_SIZE = 100;

    public string? NormalizedCode =>
        string.IsNullOrWhiteSpace(Code) ? null : Code.Trim().ToUpperInvariant();

    public string? NormalizedName =>
        string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();

    public Dictionary<string, ValidationError[]> Validate()
    {
        var errors = new Dictionary<string, ValidationError[]>();
        if (NormalizedCode?.Length > 20)
        {
            errors[nameof(Code)] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, nameof(Code), 20)];
        }

        if (NormalizedName?.Length > 200)
        {
            errors[nameof(Name)] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, nameof(Name), 200)];
        }

        if (Page < 1)
        {
            errors[nameof(Page)] = [new ValidationError(ApiErrorCodes.VALIDATION_PAGE_OUT_OF_RANGE)];
        }

        if (PageSize is < 1 or > MAX_PAGE_SIZE)
        {
            errors[nameof(PageSize)] = [new ValidationError(ApiErrorCodes.VALIDATION_PAGE_SIZE_OUT_OF_RANGE, MAX_PAGE_SIZE)];
        }

        return errors;
    }
}
