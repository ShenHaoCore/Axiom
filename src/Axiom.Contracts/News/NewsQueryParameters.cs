// 新闻列表的筛选与分页参数。
namespace Axiom.Contracts.News;

public sealed record NewsQueryParameters(
    string? Category = null,
    string? Status = null,
    string? Keyword = null,
    int Page = 1,
    int PageSize = 20)
{
    public const int MAX_PAGE_SIZE = 100;

    public string? NormalizedCategory => string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();
    public string? NormalizedStatus => string.IsNullOrWhiteSpace(Status) ? null : Status.Trim();
    public string? NormalizedKeyword => string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Page < 1)
        {
            errors[nameof(Page)] = ["页码必须大于等于 1。"];
        }

        if (PageSize < 1 || PageSize > MAX_PAGE_SIZE)
        {
            errors[nameof(PageSize)] = [$"每页条数必须在 1 到 {MAX_PAGE_SIZE} 之间。"];
        }

        return errors;
    }
}
