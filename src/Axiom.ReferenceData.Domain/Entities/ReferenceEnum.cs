// 通用枚举字典条目：按 Category+Code 唯一；Code 创建后不可改。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class ReferenceEnum
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 100;

    private ReferenceEnum()
    {
    }

    public Guid Id { get; private set; }
    public string Category { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }

    public static ReferenceEnum Create(string category, string code, string name, int sortOrder = 0)
    {
        var entity = new ReferenceEnum
        {
            Id = Guid.NewGuid(),
            Category = string.Empty,
            Code = string.Empty,
            Name = string.Empty
        };

        entity.Category = NormalizeKey(category, nameof(Category));
        entity.Code = NormalizeKey(code, nameof(Code));
        entity.Name = RequiredText(name, nameof(Name), MaxNameLength);
        entity.SortOrder = sortOrder;
        return entity;
    }

    /// <summary>更新显示名与排序；Code/Category 不可改。</summary>
    public void Update(string? name, int? sortOrder)
    {
        if (name is not null)
        {
            Name = RequiredText(name, nameof(Name), MaxNameLength);
        }

        if (sortOrder.HasValue)
        {
            SortOrder = sortOrder.Value;
        }
    }

    public static string NormalizeKey(string? value, string field)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            throw ReferenceEnumValidationException.Required(field);
        }

        if (trimmed.Length > MaxCodeLength)
        {
            throw ReferenceEnumValidationException.TooLong(field, MaxCodeLength);
        }

        return trimmed;
    }

    private static string RequiredText(string? value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw ReferenceEnumValidationException.Required(field);
        }

        if (trimmed.Length > maxLength)
        {
            throw ReferenceEnumValidationException.TooLong(field, maxLength);
        }

        return trimmed;
    }
}
