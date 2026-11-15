// 语言协商与翻译回退链（纯逻辑，不依赖 ASP.NET）。
// 协商顺序：精确匹配 → 逐级去掉子标签的父语言 → 默认语言。
namespace Axiom.SharedKernel.Localization;

using System.Text.RegularExpressions;

public static class AxiomCultureResolver
{
    private static readonly Regex CultureTagPattern = new(
        "^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 规范化 BCP-47 语言标签：去空白、下划线转连字符，语言小写、两位地区大写、四位脚本首字母大写。
    /// 空值或格式非法时返回 null。
    /// </summary>
    public static string? TryNormalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        var tag = culture.Trim().Replace('_', '-');
        if (!CultureTagPattern.IsMatch(tag))
        {
            return null;
        }

        var segments = tag.Split('-');
        var normalized = new string[segments.Length];
        normalized[0] = segments[0].ToLowerInvariant();
        for (var index = 1; index < segments.Length; index++)
        {
            var segment = segments[index];
            normalized[index] = segment.Length switch
            {
                2 => segment.ToUpperInvariant(),
                4 => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant(),
                _ => segment.ToLowerInvariant()
            };
        }

        return string.Join('-', normalized);
    }

    /// <summary>把请求语言解析为受支持的语言；无法匹配时回退到默认语言。</summary>
    public static string Resolve(
        string? requested,
        IReadOnlyCollection<string> supportedCultures,
        string defaultCulture)
    {
        var normalizedRequested = TryNormalize(requested);
        if (normalizedRequested is not null)
        {
            foreach (var candidate in ParentChain(normalizedRequested))
            {
                if (MatchSupported(candidate, supportedCultures) is { } match)
                {
                    return match;
                }
            }
        }

        return MatchSupported(TryNormalize(defaultCulture) ?? defaultCulture, supportedCultures)
               ?? defaultCulture;
    }

    /// <summary>
    /// 翻译查询的候选语言链：解析结果 → 其父语言 → 默认语言，去重且保持顺序。
    /// 链尾始终是默认语言，调用方在链也查不到时回退到主表字段值。
    /// </summary>
    public static IReadOnlyList<string> PendingChain(
        string? requested,
        IReadOnlyCollection<string> supportedCultures,
        string defaultCulture)
    {
        var chain = new List<string>();
        var resolved = Resolve(requested, supportedCultures, defaultCulture);
        Add(chain, resolved);

        foreach (var candidate in ParentChain(resolved).Skip(1))
        {
            if (MatchSupported(candidate, supportedCultures) is { } parent)
            {
                Add(chain, parent);
            }
        }

        Add(chain, MatchSupported(TryNormalize(defaultCulture) ?? defaultCulture, supportedCultures) ?? defaultCulture);
        return chain;
    }

    /// <summary>返回值是否在受支持语言列表中（按规范化结果比较）。</summary>
    public static bool IsSupported(string? culture, IReadOnlyCollection<string> supportedCultures)
    {
        var normalized = TryNormalize(culture);
        return normalized is not null && MatchSupported(normalized, supportedCultures) is not null;
    }

    private static string? MatchSupported(string candidate, IReadOnlyCollection<string> supportedCultures)
    {
        foreach (var supported in supportedCultures)
        {
            if (string.Equals(TryNormalize(supported), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        return null;
    }

    private static IEnumerable<string> ParentChain(string culture)
    {
        var parts = culture.Split('-');
        for (var length = parts.Length; length >= 1; length--)
        {
            yield return string.Join('-', parts[..length]);
        }
    }

    private static void Add(List<string> chain, string culture)
    {
        if (!chain.Any(existing => string.Equals(existing, culture, StringComparison.OrdinalIgnoreCase)))
        {
            chain.Add(culture);
        }
    }
}
