// 新闻翻译子表实体：位于聚合之外，按 (文章, 语言, 字段) 唯一。
// 主表始终保存默认语言原值，翻译仅在读取时按语言合并。
namespace Axiom.News.Domain.Entities;

public sealed class NewsArticleTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private NewsArticleTranslation()
    {
    }

    public NewsArticleTranslation(Guid newsArticleId, string culture, string field, string value)
    {
        Id = Guid.NewGuid();
        NewsArticleId = newsArticleId;
        Culture = culture;
        Field = field;
        Value = value;
    }

    public Guid Id { get; private set; }

    public Guid NewsArticleId { get; private set; }

    public string Culture { get; private set; } = null!;

    public string Field { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public void ChangeValue(string value)
    {
        Value = value;
    }
}
