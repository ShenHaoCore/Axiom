// Domain 层业务异常：行政区。
// 异常携带错误码与参数，对外消息由接口层按语言渲染。
using Axiom.SharedKernel.Errors;

namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class GeoDivisionNotFoundException(Guid id)
    : AxiomException(ApiErrorCodes.GEO_DIVISION_NOT_FOUND, $"指定的行政区不存在：{id:N}。", id);

public sealed class GeoDivisionUniqueKeyConflictException(string countryCode, int level, string code)
    : AxiomException(
        ApiErrorCodes.GEO_DIVISION_UNIQUE_KEY_CONFLICT,
        $"相同国家+层级+代码的行政区已存在：{countryCode}/L{level}/{code}。",
        countryCode, level, code);

public sealed class GeoDivisionHasChildrenException(Guid id)
    : AxiomException(ApiErrorCodes.GEO_DIVISION_HAS_CHILDREN, $"行政区 {id:N} 存在下级，无法删除。", id);

public sealed class GeoDivisionCountryNotFoundException(string countryCode)
    : AxiomException(ApiErrorCodes.GEO_DIVISION_COUNTRY_NOT_FOUND, $"国家 {countryCode} 不存在。", countryCode);

/// <summary>If-Match 的 ETag 与库中并发令牌不一致，说明记录已被其他请求修改。</summary>
public sealed class GeoDivisionConcurrencyConflictException(Guid id)
    : AxiomException(
        ApiErrorCodes.GEO_DIVISION_CONCURRENCY_CONFLICT,
        $"行政区 {id:N} 已被其他请求修改。",
        id);

/// <summary>行政区的领域不变量被破坏（国家代码、名称、坐标范围等）。</summary>
public sealed class GeoDivisionValidationException : AxiomException
{
    public GeoDivisionValidationException(string field, string errorCode, string message, params object[] args)
        : base(errorCode, message, args)
    {
        Field = field;
    }

    /// <summary>触发校验失败的字段名，供接口层构造 ProblemDetails。</summary>
    public string Field { get; }

    public static GeoDivisionValidationException Required(string field) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "该字段不能为空。", field);

    public static GeoDivisionValidationException TooLong(string field, int maxLength) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, $"长度不能超过 {maxLength} 个字符。", field, maxLength);

    public static GeoDivisionValidationException InvalidCountryCode() =>
        new("CountryCode", ApiErrorCodes.GEO_DIVISION_INVALID_COUNTRY_CODE, "国家代码必须是 2 个英文字母。");

    public static GeoDivisionValidationException LatitudeOutOfRange() =>
        new("Latitude", ApiErrorCodes.GEO_DIVISION_LATITUDE_OUT_OF_RANGE, "纬度必须位于 -90 到 90 之间。");

    public static GeoDivisionValidationException LongitudeOutOfRange() =>
        new("Longitude", ApiErrorCodes.GEO_DIVISION_LONGITUDE_OUT_OF_RANGE, "经度必须位于 -180 到 180 之间。");

    public static GeoDivisionValidationException InvalidLevel() =>
        new("Level", ApiErrorCodes.GEO_DIVISION_INVALID_LEVEL, "level 必须是不小于 1 的整数。");

    public static GeoDivisionValidationException InvalidKind() =>
        new("Kind", ApiErrorCodes.GEO_DIVISION_INVALID_KIND,
            "kind 必须是已维护的 geo_division.kind 枚举值。");

    public static GeoDivisionValidationException ParentRequired() =>
        new("ParentId", ApiErrorCodes.GEO_DIVISION_PARENT_REQUIRED, "非国家行必须指定父级行政区。");

    public static GeoDivisionValidationException ParentNotFound(Guid parentId) =>
        new("ParentId", ApiErrorCodes.GEO_DIVISION_PARENT_NOT_FOUND, $"父级行政区不存在：{parentId:N}。", parentId);

    public static GeoDivisionValidationException ParentInvalid() =>
        new("ParentId", ApiErrorCodes.GEO_DIVISION_PARENT_INVALID, "父级行政区与当前行政区的层级或国家不匹配。");

    public static GeoDivisionValidationException SubdivisionMismatch(string? expected, string actual) =>
        new("SubdivisionCode", ApiErrorCodes.GEO_DIVISION_SUBDIVISION_MISMATCH,
            $"一级区划代码必须与所属一级行政区一致（应为 {expected ?? "空"}，实际为 {actual}）。",
            expected ?? string.Empty, actual);

    public static GeoDivisionValidationException UnknownTranslatableField(string field) =>
        new(field, ApiErrorCodes.LOCALIZATION_UNSUPPORTED_FIELD, $"不支持翻译该字段：{field}。", field);
}
