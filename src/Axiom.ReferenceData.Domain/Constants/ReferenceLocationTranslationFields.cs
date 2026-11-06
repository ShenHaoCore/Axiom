// 地点档案可翻译字段白名单：名称与城市名参与多语言，代码/国家代码不翻译。
namespace Axiom.ReferenceData.Domain.Constants;

using Axiom.ReferenceData.Domain.Entities;

public static class ReferenceLocationTranslationFields
{
    public const string Name = "Name";
    public const string City = "City";

    public static IReadOnlyList<string> All { get; } = [Name, City];

    public static bool IsKnown(string field) => All.Contains(field);

    /// <summary>字段的最大长度；未知字段返回 0。</summary>
    public static int MaxLength(string field) => field switch
    {
        Name => ReferenceLocation.MaxNameLength,
        City => ReferenceLocation.MaxCityLength,
        _ => 0
    };
}
