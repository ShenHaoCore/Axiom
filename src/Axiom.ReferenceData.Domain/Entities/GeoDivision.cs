// 行政区实体（Domain 层）：聚合根，承载国家代码、编码规范化、层级与坐标范围等不变量。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class GeoDivision
{
    public const int MaxNameLength = 200;
    public const int MaxSubdivisionCodeLength = 20;
    /// <summary>树根深度；该层不允许挂父节点。</summary>
    public const int RootLevel = 1;
    /// <summary>拉树接口的深度软上限，不限制实体可存的 Level。</summary>
    public const int MaxTreeDepth = 16;

    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private GeoDivision()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ParentId { get; private set; }
    public int Level { get; private set; }
    public string Kind { get; private set; } = GeoDivisionKinds.Standard;
    public string CountryCode { get; private set; } = null!;
    public string? SubdivisionCode { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? AsciiName { get; private set; }
    public string? LocalName { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public long? Population { get; private set; }
    public int? GeoNameId { get; private set; }

    /// <summary>
    /// 创建行政区。Code 为空时以名称作为编码；根层不允许挂父节点。
    /// </summary>
    public static GeoDivision Create(
        int level,
        string countryCode,
        string name,
        string? subdivisionCode = null,
        string? code = null,
        string? asciiName = null,
        string? localName = null,
        double? latitude = null,
        double? longitude = null,
        long? population = null,
        Guid? parentId = null,
        int? geoNameId = null,
        string? kind = null)
    {
        if (level < RootLevel)
        {
            throw GeoDivisionValidationException.InvalidLevel();
        }

        var region = new GeoDivision
        {
            Id = Guid.NewGuid(),
            Level = level,
            Kind = NormalizeKind(kind),
            CountryCode = string.Empty,
            Code = string.Empty,
            Name = string.Empty
        };

        region.ApplyBase(countryCode, subdivisionCode, name, code, asciiName, localName, latitude, longitude, population, geoNameId);

        // 根层不允许挂父节点。
        region.ParentId = level == RootLevel ? null : parentId;

        return region;
    }

    /// <summary>更新行政区。参数为空表示该字段不修改。</summary>
    public void Update(
        string? name,
        string? asciiName,
        string? localName,
        string? code,
        double? latitude,
        double? longitude,
        long? population,
        Guid? parentId,
        string? kind = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = RequiredText(name, nameof(Name), MaxNameLength);
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            Code = NormalizeCode(code);
        }

        if (!string.IsNullOrWhiteSpace(asciiName))
        {
            AsciiName = asciiName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(localName))
        {
            LocalName = localName.Trim();
        }

        if (latitude.HasValue)
        {
            EnsureLatitude(latitude);
            Latitude = latitude;
        }

        if (longitude.HasValue)
        {
            EnsureLongitude(longitude);
            Longitude = longitude;
        }

        if (population.HasValue)
        {
            Population = population;
        }

        if (kind is not null)
        {
            Kind = NormalizeKind(kind);
        }

        // 根层不允许挂父节点。
        if (parentId.HasValue && Level != RootLevel)
        {
            ParentId = parentId;
        }
    }

    private static string NormalizeKind(string? kind)
    {
        var normalized = GeoDivisionKinds.Normalize(kind);
        if (normalized.Length > GeoDivisionKinds.MaxLength)
        {
            throw GeoDivisionValidationException.TooLong(nameof(Kind), GeoDivisionKinds.MaxLength);
        }

        return normalized;
    }

    private void ApplyBase(
        string countryCode,
        string? subdivisionCode,
        string name,
        string? code,
        string? asciiName,
        string? localName,
        double? latitude,
        double? longitude,
        long? population,
        int? geoNameId)
    {
        var normalizedCountry = NormalizeCode(countryCode ?? string.Empty);
        if (normalizedCountry.Length != 2 || !normalizedCountry.All(char.IsAsciiLetter))
        {
            throw GeoDivisionValidationException.InvalidCountryCode();
        }

        CountryCode = normalizedCountry;
        Name = RequiredText(name, nameof(Name), MaxNameLength);
        Code = string.IsNullOrWhiteSpace(code) ? Name : NormalizeCode(code);
        SubdivisionCode = OptionalText(subdivisionCode, nameof(SubdivisionCode), MaxSubdivisionCodeLength);
        AsciiName = OptionalText(asciiName, nameof(AsciiName), MaxNameLength);
        LocalName = OptionalText(localName, nameof(LocalName), MaxNameLength);
        EnsureLatitude(latitude);
        EnsureLongitude(longitude);
        Latitude = latitude;
        Longitude = longitude;
        Population = population;
        GeoNameId = geoNameId;
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static void EnsureLatitude(double? latitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw GeoDivisionValidationException.LatitudeOutOfRange();
        }
    }

    private static void EnsureLongitude(double? longitude)
    {
        if (longitude is < -180 or > 180)
        {
            throw GeoDivisionValidationException.LongitudeOutOfRange();
        }
    }

    private static string RequiredText(string? value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw GeoDivisionValidationException.Required(field);
        }

        if (trimmed.Length > maxLength)
        {
            throw GeoDivisionValidationException.TooLong(field, maxLength);
        }

        return trimmed;
    }

    private static string? OptionalText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw GeoDivisionValidationException.TooLong(field, maxLength);
        }

        return trimmed;
    }
}
