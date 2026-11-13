// 枚举字典可翻译字段白名单：与 reference_enum_translations 宽表列对应，当前仅 Name。
namespace Axiom.ReferenceData.Domain.Constants;

using Axiom.ReferenceData.Domain.Entities;

public static class ReferenceEnumTranslationFields
{
    public const string Name = "Name";

    public static IReadOnlyList<string> All { get; } = [Name];

    public static bool IsKnown(string field) => All.Contains(field);

    /// <summary>字段的最大长度；未知字段返回 0。</summary>
    public static int MaxLength(string field) => field == Name ? ReferenceEnum.MaxNameLength : 0;
}
