// Domain 层业务异常：地点档案。
// 异常携带错误码与参数，对外消息由接口层按语言渲染。
using Axiom.SharedKernel.Errors;

namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class ReferenceLocationNotFoundException(Guid id)
    : AxiomException(ApiErrorCodes.LOCATION_NOT_FOUND, $"指定的地点不存在：{id:N}。", id);

public sealed class ReferenceLocationCodeConflictException(string code, int type)
    : AxiomException(
        ApiErrorCodes.LOCATION_CODE_CONFLICT,
        $"相同类型的地点代码已存在：{code}（类型 {type}）。",
        code, type);

public sealed class ReferenceLocationConcurrencyConflictException(Guid id)
    : AxiomException(
        ApiErrorCodes.LOCATION_CONCURRENCY_CONFLICT,
        $"地点已被其他请求修改，请重新获取后再操作。Id={id:N}",
        id);

/// <summary>地点档案的领域不变量被破坏（代码格式、国家代码、坐标范围等）。</summary>
public sealed class ReferenceLocationValidationException : AxiomException
{
    public ReferenceLocationValidationException(string field, string errorCode, string message, params object[] args)
        : base(errorCode, message, args)
    {
        Field = field;
    }

    /// <summary>触发校验失败的字段名，供接口层构造 ProblemDetails。</summary>
    public string Field { get; }

    public static ReferenceLocationValidationException Required(string field) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "该字段不能为空。", field);

    public static ReferenceLocationValidationException TooLong(string field, int maxLength) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, $"长度不能超过 {maxLength} 个字符。", field, maxLength);

    public static ReferenceLocationValidationException InvalidCountryCode() =>
        new("CountryCode", ApiErrorCodes.LOCATION_INVALID_COUNTRY_CODE, "国家代码必须是 2 个英文字母。");

    public static ReferenceLocationValidationException LatitudeOutOfRange() =>
        new("Latitude", ApiErrorCodes.LOCATION_LATITUDE_OUT_OF_RANGE, "纬度必须位于 -90 到 90 之间。");

    public static ReferenceLocationValidationException LongitudeOutOfRange() =>
        new("Longitude", ApiErrorCodes.LOCATION_LONGITUDE_OUT_OF_RANGE, "经度必须位于 -180 到 180 之间。");

    public static ReferenceLocationValidationException UnknownTranslatableField(string field) =>
        new(field, ApiErrorCodes.LOCALIZATION_UNSUPPORTED_FIELD, $"不支持翻译该字段：{field}。", field);
}
