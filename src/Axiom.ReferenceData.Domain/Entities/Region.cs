// 行政区实体（Domain 层）：聚合根，承载国家代码、编码规范化、层级与坐标范围等不变量。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class Region
{
    public const int MaxNameLength = 200;
    public const int MaxAdmin1CodeLength = 20;

    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private Region()
    {
    }

    public Guid Id { get; private set; }
    public Guid? ParentId { get; private set; }
    public RegionLevel Level { get; private set; }
    public string CountryCode { get; private set; } = null!;
    public string? Admin1Code { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? AsciiName { get; private set; }
    public string? LocalName { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public long? Population { get; private set; }
    public int? GeoNameId { get; private set; }

    /// <summary>
    /// 创建行政区。Code 为空时以名称作为编码；国家级别不允许挂父节点。
    /// </summary>
    public static Region Create(
        RegionLevel level,
        string countryCode,
        string name,
        string? admin1Code = null,
        string? code = null,
        string? asciiName = null,
        string? localName = null,
        double? latitude = null,
        double? longitude = null,
        long? population = null,
        Guid? parentId = null,
        int? geoNameId = null)
    {
        var region = new Region
        {
            Id = Guid.NewGuid(),
            Level = level,
            CountryCode = string.Empty,
            Code = string.Empty,
            Name = string.Empty
        };

        region.ApplyBase(countryCode, admin1Code, name, code, asciiName, localName, latitude, longitude, population, geoNameId);

        if (level == RegionLevel.Country)
        {
            // 国家是层级树根，忽略父节点，避免构造出非法层级。
            region.ParentId = null;
        }
        else
        {
            region.ParentId = parentId;
        }

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
        Guid? parentId)
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

        // 国家是层级树根，不允许挂父节点。
        if (parentId.HasValue && Level != RegionLevel.Country)
        {
            ParentId = parentId;
        }
    }

    private void ApplyBase(
        string countryCode,
        string? admin1Code,
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
            throw RegionValidationException.InvalidCountryCode();
        }

        CountryCode = normalizedCountry;
        Name = RequiredText(name, nameof(Name), MaxNameLength);
        Code = string.IsNullOrWhiteSpace(code) ? Name : NormalizeCode(code);
        Admin1Code = OptionalText(admin1Code, nameof(Admin1Code), MaxAdmin1CodeLength);
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
            throw RegionValidationException.LatitudeOutOfRange();
        }
    }

    private static void EnsureLongitude(double? longitude)
    {
        if (longitude is < -180 or > 180)
        {
            throw RegionValidationException.LongitudeOutOfRange();
        }
    }

    private static string RequiredText(string? value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw RegionValidationException.Required(field);
        }

        if (trimmed.Length > maxLength)
        {
            throw RegionValidationException.TooLong(field, maxLength);
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
            throw RegionValidationException.TooLong(field, maxLength);
        }

        return trimmed;
    }
}
