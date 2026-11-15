// 地点档案翻译子表实体：位于聚合之外，按 (地点, 语言, 字段) 唯一。
namespace Axiom.ReferenceData.Domain.Entities;

public sealed class ReferenceLocationTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private ReferenceLocationTranslation()
    {
    }

    public ReferenceLocationTranslation(Guid referenceLocationId, string culture, string field, string value)
    {
        Id = Guid.NewGuid();
        ReferenceLocationId = referenceLocationId;
        Culture = culture;
        Field = field;
        Value = value;
    }

    public Guid Id { get; private set; }

    public Guid ReferenceLocationId { get; private set; }

    public string Culture { get; private set; } = null!;

    public string Field { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public void ChangeValue(string value)
    {
        Value = value;
    }
}
