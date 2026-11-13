// 翻译子资源的读写模型：网关返回的译文集合与各页面的录入表单。
namespace Axiom.Web.Models;

/// <summary>某一实体在单一语言下的译文集合（字段名 → 译文）。</summary>
public sealed record TranslationView(string Culture, IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string field) => Values.TryGetValue(field, out var value) ? value : null;
}

/// <summary>新闻编辑页的译文输入。</summary>
public sealed class NewsTranslationInput
{
    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Content { get; set; }

    public static NewsTranslationInput From(TranslationView? view) => new()
    {
        Title = view?.Get("Title"),
        Summary = view?.Get("Summary"),
        Content = view?.Get("Content")
    };

    public Dictionary<string, string> ToValues() =>
        TranslationValues.Create(("Title", Title), ("Summary", Summary), ("Content", Content));
}

/// <summary>机场翻译页的译文输入。</summary>
public sealed class AirportTranslationInput
{
    public string? Name { get; set; }

    public string? City { get; set; }

    public static AirportTranslationInput From(TranslationView? view) => new()
    {
        Name = view?.Get("Name"),
        City = view?.Get("City")
    };

    public Dictionary<string, string> ToValues() =>
        TranslationValues.Create(("Name", Name), ("City", City));
}

/// <summary>行政区详情页的译文输入。</summary>
public sealed class GeoDivisionTranslationInput
{
    public string? Name { get; set; }

    public static GeoDivisionTranslationInput From(TranslationView? view) => new()
    {
        Name = view?.Get("Name")
    };

    public Dictionary<string, string> ToValues() =>
        TranslationValues.Create(("Name", Name));
}

/// <summary>枚举字典编辑页的译文输入（仅 Name）。</summary>
public sealed class ReferenceEnumTranslationInput
{
    public string? Name { get; set; }

    public static ReferenceEnumTranslationInput From(TranslationView? view) => new()
    {
        Name = view?.Get("Name")
    };

    public Dictionary<string, string> ToValues() =>
        TranslationValues.Create(("Name", Name));
}

/// <summary>收集非空译文；未出现的字段由服务端视为删除。</summary>
public static class TranslationValues
{
    public static Dictionary<string, string> Create(params (string Field, string? Value)[] fields)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in fields)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[field] = value.Trim();
            }
        }

        return values;
    }
}
