// 行政区翻译子表实体：位于聚合之外，按 (国家, 区划代码, 语言, 字段) 唯一。
namespace Axiom.ReferenceData.Domain.Entities;

public sealed class RegionTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private RegionTranslation()
    {
    }

    public RegionTranslation(string regionCountryCode, string regionCode, string culture, string field, string value)
    {
        RegionCountryCode = regionCountryCode;
        RegionCode = regionCode;
        Culture = culture;
        Field = field;
        Value = value;
    }

    /// <summary>复合业务键之一：ISO 3166-1 alpha-2。</summary>
    public string RegionCountryCode { get; private set; } = null!;

    /// <summary>复合业务键之二：区划代码。</summary>
    public string RegionCode { get; private set; } = null!;

    public string Culture { get; private set; } = null!;

    public string Field { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public void ChangeValue(string value)
    {
        Value = value;
    }
}
