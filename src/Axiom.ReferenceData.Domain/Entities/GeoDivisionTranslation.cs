// 行政区翻译：一行一语言，当前仅 Name；与主表默认名通过语言回退链合并。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class GeoDivisionTranslation
{
    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private GeoDivisionTranslation()
    {
    }

    public GeoDivisionTranslation(Guid geoDivisionId, string culture, string name)
    {
        Id = Guid.NewGuid();
        GeoDivisionId = geoDivisionId;
        Culture = NormalizeCulture(culture);
        Name = RequiredName(name);
    }

    public Guid Id { get; private set; }

    public Guid GeoDivisionId { get; private set; }

    public string Culture { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public void ChangeName(string name) => Name = RequiredName(name);

    private static string NormalizeCulture(string culture)
    {
        var trimmed = (culture ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > 16)
        {
            throw GeoDivisionValidationException.TooLong("Culture", 16);
        }

        return trimmed;
    }

    private static string RequiredName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw GeoDivisionValidationException.Required(nameof(Name));
        }

        if (trimmed.Length > GeoDivision.MaxNameLength)
        {
            throw GeoDivisionValidationException.TooLong(nameof(Name), GeoDivision.MaxNameLength);
        }

        return trimmed;
    }
}
