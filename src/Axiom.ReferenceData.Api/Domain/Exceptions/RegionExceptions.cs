// Domain 层业务异常：行政区。
namespace Axiom.ReferenceData.Domain.Exceptions;

public sealed class RegionNotFoundException(Guid id)
    : Exception($"指定的行政区不存在：{id:N}。");

public sealed class RegionUniqueKeyConflictException(string countryCode, int level, string code)
    : Exception($"相同国家+层级+代码的行政区已存在：{countryCode}/L{level}/{code}。");

public sealed class RegionHasChildrenException(Guid id)
    : Exception($"行政区 {id:N} 存在下级，无法删除。");

public sealed class RegionCountryNotFoundException(string countryCode)
    : Exception($"国家 {countryCode} 不存在。");
