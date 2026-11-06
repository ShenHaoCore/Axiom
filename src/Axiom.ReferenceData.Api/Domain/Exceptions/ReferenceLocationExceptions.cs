// Domain 层业务异常：地点档案。
namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class ReferenceLocationNotFoundException(Guid id)
    : Exception($"指定的地点不存在：{id:N}。");

public sealed class ReferenceLocationCodeConflictException(string code, int type)
    : Exception($"相同类型的地点代码已存在：{code}。");

public sealed class ReferenceLocationConcurrencyConflictException(Guid id)
    : Exception($"地点已被其他请求修改，请重新获取后再操作。Id={id:N}");
