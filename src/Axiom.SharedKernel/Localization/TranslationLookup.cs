// 翻译结果的优先级查找：按候选语言链，先命中的语言覆盖后命中的语言。
namespace Axiom.SharedKernel.Localization;

/// <summary>翻译表中最小的行：实体、语言、字段、值。</summary>
public readonly record struct TranslationValue(string EntityId, string Culture, string Field, string Value);

public sealed class TranslationLookup
{
    private readonly Dictionary<(string EntityId, string Field), string> values;

    private TranslationLookup(Dictionary<(string EntityId, string Field), string> values)
    {
        this.values = values;
    }

    public static TranslationLookup Empty { get; } = new([]);

    /// <summary>把翻译行按候选语言链合并成 (实体, 字段) → 值 的查找表。</summary>
    public static TranslationLookup Create(
        IEnumerable<TranslationValue> rows,
        IReadOnlyList<string> cultureChain)
    {
        var byCulture = new Dictionary<string, List<TranslationValue>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            byCulture.TryAdd(row.Culture, []);
            byCulture[row.Culture].Add(row);
        }

        var merged = new Dictionary<(string, string), string>();
        foreach (var culture in cultureChain)
        {
            if (!byCulture.TryGetValue(culture, out var cultureRows))
            {
                continue;
            }

            foreach (var row in cultureRows)
            {
                merged.TryAdd((row.EntityId, row.Field), row.Value);
            }
        }

        return new TranslationLookup(merged);
    }

    /// <summary>取翻译值；没有翻译时返回 null，由调用方回退到主表字段。</summary>
    public string? Find(string entityId, string field) =>
        values.TryGetValue((entityId, field), out var value) ? value : null;
}
