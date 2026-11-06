// 地点档案实体（Domain 层）：聚合根，承载代码格式、国家代码与坐标范围等不变量。
// ConcurrencyToken 是 EF Core rowversion 级别的并发令牌。
using Axiom.ReferenceData.Domain.Exceptions;

namespace Axiom.ReferenceData.Domain.Entities;

public sealed class ReferenceLocation
{
    public const int MaxNameLength = 200;
    public const int MaxCityLength = 100;

    // 供 EF Core 物化实体使用，不应在业务代码中调用。
    private ReferenceLocation()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();
    public ReferenceLocationType Type { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string CountryCode { get; private set; } = null!;
    public string? City { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>创建地点档案。所有格式与范围校验由实体负责。</summary>
    public static ReferenceLocation Create(
        ReferenceLocationType type,
        string code,
        string name,
        string countryCode,
        string? city = null,
        double? latitude = null,
        double? longitude = null)
    {
        var location = new ReferenceLocation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Code = string.Empty,
            Name = string.Empty,
            CountryCode = string.Empty
        };

        location.Apply(code, name, countryCode, city, latitude, longitude);
        return location;
    }

    /// <summary>全量更新地点档案（PUT 语义）。</summary>
    public void Update(
        string code,
        string name,
        string countryCode,
        string? city,
        double? latitude,
        double? longitude)
    {
        Apply(code, name, countryCode, city, latitude, longitude);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>按类型校验代码格式：机场 3 位字母、港口 5 位字母数字、火车站 1-20 位字母数字与连字符。</summary>
    public static bool IsValidCode(ReferenceLocationType type, string code) => type switch
    {
        ReferenceLocationType.Airport => code.Length == 3 && code.All(char.IsAsciiLetter),
        ReferenceLocationType.Seaport => code.Length == 5 && code.All(char.IsAsciiLetterOrDigit),
        ReferenceLocationType.RailStation => code.Length is >= 1 and <= 20 &&
            code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'),
        _ => false
    };

    /// <summary>返回按类型对应的代码格式提示，供接口层构造校验错误。</summary>
    public static string GetCodeFormatError(ReferenceLocationType type) => type switch
    {
        ReferenceLocationType.Airport => "机场代码必须是 3 个英文字母。",
        ReferenceLocationType.Seaport => "港口代码必须是 5 位字母或数字组成的 UN/LOCODE。",
        ReferenceLocationType.RailStation => "火车站代码只能包含字母、数字和连字符，且不能超过 20 个字符。",
        _ => "不支持的基础档案类型。"
    };

    /// <summary>代码规范化：去空格并转大写。</summary>
    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private void Apply(
        string code,
        string name,
        string countryCode,
        string? city,
        double? latitude,
        double? longitude)
    {
        var normalizedCode = NormalizeCode(code ?? string.Empty);
        if (!IsValidCode(Type, normalizedCode))
        {
            throw new ReferenceLocationValidationException(nameof(Code), GetCodeFormatError(Type));
        }

        var normalizedCountry = NormalizeCode(countryCode ?? string.Empty);
        if (normalizedCountry.Length != 2 || !normalizedCountry.All(char.IsAsciiLetter))
        {
            throw new ReferenceLocationValidationException(nameof(CountryCode), "国家代码必须是 2 个英文字母。");
        }

        Code = normalizedCode;
        Name = RequiredText(name, nameof(Name), MaxNameLength, "名称必填。");
        CountryCode = normalizedCountry;
        City = OptionalText(city, nameof(City), MaxCityLength);
        EnsureCoordinates(latitude, longitude);
        Latitude = latitude;
        Longitude = longitude;
    }

    private static void EnsureCoordinates(double? latitude, double? longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ReferenceLocationValidationException(nameof(Latitude), "纬度必须位于 -90 到 90 之间。");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ReferenceLocationValidationException(nameof(Longitude), "经度必须位于 -180 到 180 之间。");
        }
    }

    private static string RequiredText(string? value, string field, int maxLength, string emptyMessage)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ReferenceLocationValidationException(field, emptyMessage);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ReferenceLocationValidationException(field, $"长度不能超过 {maxLength} 个字符。");
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
            throw new ReferenceLocationValidationException(field, $"长度不能超过 {maxLength} 个字符。");
        }

        return trimmed;
    }
}
