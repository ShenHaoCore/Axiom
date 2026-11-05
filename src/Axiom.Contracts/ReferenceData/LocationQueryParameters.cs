// 定义地点列表共享的筛选、分页参数及输入边界。
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

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (NormalizedCode?.Length > 20)
        {
            errors[nameof(Code)] = ["代码不能超过 20 个字符。"];
        }

        if (NormalizedName?.Length > 200)
        {
            errors[nameof(Name)] = ["名称不能超过 200 个字符。"];
        }

        if (Page < 1)
        {
            errors[nameof(Page)] = ["页码必须大于或等于 1。"];
        }

        if (PageSize is < 1 or > MAX_PAGE_SIZE)
        {
            errors[nameof(PageSize)] = [$"页大小必须在 1 到 {MAX_PAGE_SIZE} 之间。"];
        }

        return errors;
    }
}
