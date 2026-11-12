// 行政区 Kind 种子 code：须先写入 reference_enums（Category = geo_division.kind）。
namespace Axiom.ReferenceData.Domain.Constants;

public static class GeoDivisionKinds
{
    public const int MaxLength = 64;

    public const string Standard = "standard";
    public const string Municipality = "municipality";
    public const string AutonomousRegion = "autonomous_region";
    public const string SpecialAdministrativeRegion = "special_administrative_region";

    /// <summary>种子与默认下拉用的约定项（显示名由字典表维护）。</summary>
    public static IReadOnlyList<(string Code, string Name, int SortOrder)> SeedItems { get; } =
    [
        (Standard, "标准", 0),
        (Municipality, "直辖市", 1),
        (AutonomousRegion, "自治区", 2),
        (SpecialAdministrativeRegion, "特别行政区", 3)
    ];

    public static string Normalize(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return Standard;
        }

        return kind.Trim().ToLowerInvariant();
    }
}
