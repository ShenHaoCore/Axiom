// 枚举字典翻译：一行一语言，当前仅 Name；与主表默认名通过语言回退链合并。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class ReferenceEnumTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private ReferenceEnumTranslation()
    {
    }

    public ReferenceEnumTranslation(Guid referenceEnumId, string culture, string name)
    {
        Id = Guid.NewGuid();
        ReferenceEnumId = referenceEnumId;
        Culture = NormalizeCulture(culture);
        Name = RequiredName(name);
    }

    public Guid Id { get; private set; }

    public Guid ReferenceEnumId { get; private set; }

    public string Culture { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public void ChangeName(string name) => Name = RequiredName(name);

    private static string NormalizeCulture(string culture)
    {
        var trimmed = (culture ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > 16)
        {
            throw ReferenceEnumValidationException.TooLong("Culture", 16);
        }

        return trimmed;
    }

    private static string RequiredName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw ReferenceEnumValidationException.Required(nameof(Name));
        }

        if (trimmed.Length > ReferenceEnum.MaxNameLength)
        {
            throw ReferenceEnumValidationException.TooLong(nameof(Name), ReferenceEnum.MaxNameLength);
        }

        return trimmed;
    }
}
