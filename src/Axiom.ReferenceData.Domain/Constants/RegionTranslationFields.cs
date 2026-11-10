// 行政区可翻译字段白名单：当前仅名称参与多语言。
namespace Axiom.ReferenceData.Domain.Constants;

using Axiom.ReferenceData.Domain.Entities;

public static class RegionTranslationFields
{
    public const string Name = "Name";

    public static IReadOnlyList<string> All { get; } = [Name];

    public static bool IsKnown(string field) => All.Contains(field);

    /// <summary>字段的最大长度；未知字段返回 0。</summary>
    public static int MaxLength(string field) => field == Name ? Region.MaxNameLength : 0;
}
