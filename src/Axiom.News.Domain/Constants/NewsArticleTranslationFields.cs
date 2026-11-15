// 新闻可翻译字段白名单：只有内容类字段参与多语言，分类/作者等键值字段不翻译。
namespace Axiom.News.Domain.Constants;

public static class NewsArticleTranslationFields
{
    public const string Title = "Title";
    public const string Summary = "Summary";
    public const string Content = "Content";

    public static IReadOnlyList<string> All { get; } = [Title, Summary, Content];

    public static bool IsKnown(string field) => All.Contains(field);

    /// <summary>字段的最大长度；未知字段返回 0。</summary>
    public static int MaxLength(string field) => field switch
    {
        Title => NewsArticleLengths.MaxTitleLength,
        Summary => NewsArticleLengths.MaxSummaryLength,
        Content => NewsArticleLengths.MaxContentLength,
        _ => 0
    };
}
