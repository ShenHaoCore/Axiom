// 开发 Mock 的语言协商与译文合并：按 Accept-Language 回退链套用已保存的译文，未命中字段回退主表值。
using Axiom.SharedKernel.Localization;
using Axiom.Web.Models;

namespace Axiom.Web.Mocking;

internal sealed class MockLocalizer(AxiomLocalizationOptions localization)
{
    // 与真实网关一致的语言协商规则，供列表/详情套用已保存的译文。
    internal string ResolveCulture(HttpRequestMessage request)
    {
        var requested = request.Headers.AcceptLanguage
            .Select(language => language.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return localization.CultureChain(requested)[0];
    }

    // 按语言链合并译文，先命中的语言优先；未命中的字段由调用方回退主表值。
    internal Dictionary<string, string> FindTranslations(string kind, Guid id, string culture)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        lock (MockDataStore.Gate)
        {
            foreach (var candidate in localization.CultureChain(culture))
            {
                if (MockDataStore.Translations.TryGetValue($"{kind}/{id}/{candidate}", out var values))
                {
                    foreach (var (field, value) in values)
                    {
                        merged.TryAdd(field, value);
                    }
                }
            }
        }

        return merged;
    }

    // 空译文视为未翻译，保持与真实网关"回退主表"一致的行为。
    private static string? Pick(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    internal AirportResponse Localize(AirportResponse airport, string culture)
    {
        var values = FindTranslations("airport", airport.Id, culture);
        return airport with
        {
            Name = Pick(values, "Name") ?? airport.Name,
            City = Pick(values, "City") ?? airport.City
        };
    }

    internal NewsArticleView Localize(NewsArticleView article, string culture)
    {
        var values = FindTranslations("news", article.Id, culture);
        return article with
        {
            Title = Pick(values, "Title") ?? article.Title,
            Summary = Pick(values, "Summary") ?? article.Summary,
            Content = Pick(values, "Content") ?? article.Content
        };
    }

    internal GeoDivisionView Localize(GeoDivisionView region, string culture)
    {
        var values = FindTranslations("region", region.Id, culture);
        return region with { Name = Pick(values, "Name") ?? region.Name };
    }

    internal ReferenceEnumView Localize(ReferenceEnumView item, string culture)
    {
        var values = FindTranslations("enum", item.Id, culture);
        return item with { Name = Pick(values, "Name") ?? item.Name };
    }

    internal GeoDivisionTreeView Localize(GeoDivisionTreeView node, string culture)
    {
        var values = FindTranslations("region", node.Id, culture);
        return node with
        {
            Name = Pick(values, "Name") ?? node.Name,
            Children = node.Children.Select(child => Localize(child, culture)).ToList()
        };
    }
}
