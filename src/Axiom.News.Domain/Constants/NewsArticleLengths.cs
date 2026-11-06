// Domain 层定义的字段长度常量，供 Infrastructure（EF Core 配置）和 Application（输入校验）共用。
namespace Axiom.News.Domain.Constants;

public static class NewsArticleLengths
{
    public const int MaxTitleLength = 200;
    public const int MaxSummaryLength = 500;
    public const int MaxContentLength = 50000;
    public const int MaxCategoryLength = 50;
    public const int MaxAuthorLength = 100;
}
