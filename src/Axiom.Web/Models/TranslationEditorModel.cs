// 译文编辑器的视图模型：按语言分组，并携带默认语言原文供参考。
using Axiom.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Models;

/// <summary>译文编辑器：源文说明 + 每个语言一个页签。</summary>
public sealed record TranslationEditorModel(
    string SourceLanguage,
    IReadOnlyList<TranslationLocaleSection> Locales)
{
    /// <summary>默认语言原文；各语言的字段结构一致，取第一个语言的源值即可。</summary>
    public IReadOnlyList<TranslationField> SourceFields =>
        Locales.Count == 0 ? [] : Locales[0].Fields;

    /// <summary>已完成翻译的语言数。</summary>
    public int TranslatedCount => Locales.Count(locale => locale.IsTranslated);
}

/// <summary>单个语言的页签内容与翻译状态。</summary>
public sealed record TranslationLocaleSection(
    string Culture,
    string Label,
    bool IsTranslated,
    IReadOnlyList<TranslationField> Fields);

/// <summary>译文编辑器中的单个字段：字段名对应 <c>Translations[culture].Name</c> 绑定路径。</summary>
public sealed record TranslationField(
    string Label,
    string Name,
    string? Value,
    string? SourceValue,
    bool Multiline = false,
    int? MaxLength = null);

/// <summary>构建译文编辑器的公共逻辑：语言名回退、翻译状态判定。</summary>
public static class TranslationEditorBuilder
{
    /// <summary>取语言显示名；资源缺失时回退为语言代码。</summary>
    public static string CultureLabel(string culture, IStringLocalizer<SharedResource> localizer)
    {
        var resource = localizer[$"Culture.{culture}"];
        return resource.ResourceNotFound ? culture : resource.Value;
    }

    /// <summary>任一字段有译文即视为该语言已翻译。</summary>
    public static TranslationLocaleSection CreateSection(
        string culture,
        IStringLocalizer<SharedResource> localizer,
        params TranslationField[] fields) => new(
            culture,
            CultureLabel(culture, localizer),
            fields.Any(field => !string.IsNullOrWhiteSpace(field.Value)),
            fields);
}
