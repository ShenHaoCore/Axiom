// 行政区实体（Domain 层）：对齐 geo_division 数据集的结构——
// 复合业务主键 (CountryCode, Code)、ParentCode 复合外键自连、Depth 树深度、
// 国别口径 Type 与 Status 生命周期。国家行（Depth = 0）以 ISO 代码作为 Code。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class Region
{
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 50;
    public const int MaxTypeLength = 32;
    public const int MaxStatusLength = 16;
    public const int MaxCodeIsoLength = 16;
    public const int MaxSortKeyLength = 32;
    public const int MaxAbbrevLength = 16;

    /// <summary>国家行的固定类型（geo_division 数据集中国家位于独立数据集，这里并入同表）。</summary>
    public const string TypeCountry = "country";

    /// <summary>无法归入国别口径时的兜底类型。</summary>
    public const string TypeOther = "other";

    public const string StatusActive = "active";
    public const string StatusDeprecated = "deprecated";

    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private Region()
    {
    }

    /// <summary>ISO 3166-1 alpha-2，复合主键之一。</summary>
    public string CountryCode { get; private set; } = null!;

    /// <summary>国别内唯一的区划代码，复合主键之二；国家行使用 ISO 代码。</summary>
    public string Code { get; private set; } = null!;

    /// <summary>上级区划代码；Depth 为 0 的国家行为 null。与 CountryCode 组成复合外键。</summary>
    public string? ParentCode { get; private set; }

    /// <summary>树深度：国家=0，省=1，地级=2，县级=3（对齐数据集口径，国家行为适配偏移）。</summary>
    public int Depth { get; private set; }

    /// <summary>国别口径的机器可读类型：province/municipality/autonomous_region/sar/prefecture_city/county 等。</summary>
    public string Type { get; private set; } = null!;

    /// <summary>生命周期状态：active / deprecated（行政区划撤并后保留历史行）。</summary>
    public string Status { get; private set; } = null!;

    /// <summary>ISO 3166-2 代码（如 CN-11）；无则为 null。</summary>
    public string? CodeIso { get; private set; }

    /// <summary>国别排序键（定长补齐等）；空字符串表示未指定。</summary>
    public string SortKey { get; private set; } = string.Empty;

    /// <summary>缩写（如美国州 USPS 代码）；空字符串表示未指定。</summary>
    public string Abbrev { get; private set; } = string.Empty;

    public string Name { get; private set; } = null!;
    public string? AsciiName { get; private set; }
    public string? LocalName { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public long? Population { get; private set; }
    public int? GeoNameId { get; private set; }

    /// <summary>父级行政区导航；国家行（Depth = 0）为 null。</summary>
    public Region? Parent { get; private set; }
    private readonly List<Region> children = [];

    /// <summary>直接子级行政区（按需加载，惰性树不依赖该集合）。</summary>
    public IReadOnlyList<Region> Children => children.AsReadOnly();

    /// <summary>
    /// 创建行政区。国家行（depth = 0）以国家代码作为 Code 且不允许挂父节点；
    /// 其余行必须给出上级代码，类型缺省时按深度推导（province/prefecture_city/district）。
    /// </summary>
    public static Region Create(
        int depth,
        string countryCode,
        string code,
        string name,
        string? parentCode = null,
        string? type = null,
        string? codeIso = null,
        string? sortKey = null,
        string? abbrev = null,
        string? asciiName = null,
        string? localName = null,
        double? latitude = null,
        double? longitude = null,
        long? population = null,
        int? geoNameId = null,
        string? status = null)
    {
        var region = new Region
        {
            CountryCode = string.Empty,
            Code = string.Empty,
            Name = string.Empty,
            Type = string.Empty,
            Status = string.Empty
        };

        region.ApplyBase(countryCode, code, name, asciiName, localName, latitude, longitude, population, geoNameId);
        region.ApplyHierarchy(depth, parentCode, type, status);
        region.CodeIso = OptionalText(codeIso, nameof(CodeIso), MaxCodeIsoLength);
        region.SortKey = OptionalText(sortKey, nameof(SortKey), MaxSortKeyLength) ?? string.Empty;
        region.Abbrev = OptionalText(abbrev, nameof(Abbrev), MaxAbbrevLength) ?? string.Empty;
        return region;
    }

    /// <summary>更新行政区。Code / Depth / Type 为业务标识不做修改；参数为空表示对应字段不修改。</summary>
    public void Update(
        string? name,
        string? asciiName,
        string? localName,
        double? latitude,
        double? longitude,
        long? population,
        string? codeIso,
        string? sortKey,
        string? abbrev,
        string? status = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = RequiredText(name, nameof(Name), MaxNameLength);
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

        if (codeIso is not null)
        {
            CodeIso = OptionalText(codeIso, nameof(CodeIso), MaxCodeIsoLength);
        }

        if (sortKey is not null)
        {
            SortKey = OptionalText(sortKey, nameof(SortKey), MaxSortKeyLength) ?? string.Empty;
        }

        if (abbrev is not null)
        {
            Abbrev = OptionalText(abbrev, nameof(Abbrev), MaxAbbrevLength) ?? string.Empty;
        }

        if (status is not null)
        {
            var normalized = NormalizeCode(status).ToLowerInvariant();
            if (normalized is not StatusActive and not StatusDeprecated)
            {
                throw RegionValidationException.InvalidStatus();
            }

            Status = normalized;
        }
    }

    /// <summary>标记为已撤销（行政区划撤并后保留历史行，不做物理删除时使用）。</summary>
    public void Deprecate() => Status = StatusDeprecated;

    private void ApplyHierarchy(int depth, string? parentCode, string? type, string? status)
    {
        if (depth is < 0 or > 3)
        {
            throw RegionValidationException.InvalidDepth();
        }

        Depth = depth;
        ParentCode = depth == 0 ? null : NormalizeCode(parentCode ?? string.Empty) is var normalized && normalized.Length > 0
            ? normalized
            : throw RegionValidationException.Required(nameof(ParentCode));
        // type / status 是小写机器值（对齐 geo_division 口径），归一化为小写而非大写。
        var trimmedType = (type ?? string.Empty).Trim();
        if (trimmedType.Length > MaxTypeLength)
        {
            throw RegionValidationException.TooLong(nameof(Type), MaxTypeLength);
        }

        Type = trimmedType.Length > 0
            ? trimmedType.ToLowerInvariant()
            : depth == 0 ? TypeCountry : DefaultType(depth);
        Status = NormalizeCode(status ?? string.Empty) is var normalizedStatus && normalizedStatus.Length > 0
            ? normalizedStatus.ToLowerInvariant()
            : StatusActive;

        if (Status is not StatusActive and not StatusDeprecated)
        {
            throw RegionValidationException.InvalidStatus();
        }
    }

    private static string DefaultType(int depth) => depth switch
    {
        1 => "province",
        2 => "prefecture_city",
        _ => "district"
    };

    private void ApplyBase(
        string countryCode,
        string code,
        string name,
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
        Code = RequiredText(code, nameof(Code), MaxCodeLength);
        Name = RequiredText(name, nameof(Name), MaxNameLength);
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
