// 行政区翻译子表实体：位于聚合之外，按 (行政区, 语言, 字段) 唯一。
namespace Axiom.ReferenceData.Domain.Entities;

public sealed class RegionTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private RegionTranslation()
    {
    }

    public RegionTranslation(Guid regionId, string culture, string field, string value)
    {
        Id = Guid.NewGuid();
        RegionId = regionId;
        Culture = culture;
        Field = field;
        Value = value;
    }

    public Guid Id { get; private set; }

    public Guid RegionId { get; private set; }

    public string Culture { get; private set; } = null!;

    public string Field { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public void ChangeValue(string value)
    {
        Value = value;
    }
}
