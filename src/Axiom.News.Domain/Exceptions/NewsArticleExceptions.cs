// Domain 层定义的业务异常。
// 基础设施层可以捕获 EF Core 异常并转换为这些领域异常，让上层代码不依赖 EF Core。

namespace Axiom.News.Domain.Exceptions;

/// <summary>请求的新闻不存在（404）。</summary>
public sealed class NewsArticleNotFoundException(Guid id)
    : Exception($"指定的新闻不存在：{id:N}。");

/// <summary>并发冲突——资源已被其他请求修改（409）。</summary>
public sealed class NewsArticleConcurrencyConflictException(Guid id)
    : Exception($"新闻已被其他请求修改，请重新获取后再操作。Id={id:N}");

/// <summary>缺少或格式不正确的 If-Match 头（428 / 400）。</summary>
public sealed class MissingOrInvalidETagException(string message)
    : Exception(message);

/// <summary>领域不变量被破坏——字段为空、超长或状态非法（400）。</summary>
public sealed class NewsArticleValidationException(string field, string message)
    : Exception(message)
{
    /// <summary>触发校验失败的字段名，供接口层构造 ProblemDetails 时使用。</summary>
    public string Field { get; } = field;
}
