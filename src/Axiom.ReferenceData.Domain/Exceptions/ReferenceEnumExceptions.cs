// Domain 层业务异常：通用枚举字典。
using Axiom.SharedKernel.Errors;

namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class ReferenceEnumNotFoundException(Guid id)
    : AxiomException(ApiErrorCodes.REFERENCE_ENUM_NOT_FOUND, $"指定的枚举项不存在：{id:N}。", id);

public sealed class ReferenceEnumCodeConflictException(string category, string code)
    : AxiomException(
        ApiErrorCodes.REFERENCE_ENUM_CODE_CONFLICT,
        $"相同分类+代码的枚举项已存在：{category}/{code}。",
        category, code);

public sealed class ReferenceEnumInUseException(string category, string code)
    : AxiomException(
        ApiErrorCodes.REFERENCE_ENUM_IN_USE,
        $"枚举项仍被引用，无法删除：{category}/{code}。",
        category, code);

public sealed class ReferenceEnumValidationException : AxiomException
{
    public ReferenceEnumValidationException(string field, string errorCode, string message, params object[] args)
        : base(errorCode, message, args)
    {
        Field = field;
    }

    public string Field { get; }

    public static ReferenceEnumValidationException Required(string field) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "该字段不能为空。", field);

    public static ReferenceEnumValidationException TooLong(string field, int maxLength) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, $"长度不能超过 {maxLength} 个字符。", field, maxLength);
}
