// Domain 层业务异常：地点档案。
namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class ReferenceLocationNotFoundException(Guid id)
    : Exception($"指定的地点不存在：{id:N}。");

public sealed class ReferenceLocationCodeConflictException(string code, int type)
    : Exception($"相同类型的地点代码已存在：{code}（类型 {type}）。");

public sealed class ReferenceLocationConcurrencyConflictException(Guid id)
    : Exception($"地点已被其他请求修改，请重新获取后再操作。Id={id:N}");

/// <summary>地点档案的领域不变量被破坏（代码格式、国家代码、坐标范围等）。</summary>
public sealed class ReferenceLocationValidationException(string field, string message)
    : Exception(message)
{
    /// <summary>触发校验失败的字段名，供接口层构造 ProblemDetails。</summary>
    public string Field { get; } = field;
}
