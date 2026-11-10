// Domain 层定义的业务异常。
// 基础设施层可以捕获 EF Core 异常并转换为这些领域异常，让上层代码不依赖 EF Core。
// 异常携带错误码与参数，Message 仅作日志/测试用的默认语言文本。
using Axiom.SharedKernel.Errors;

namespace Axiom.News.Domain.Exceptions;

/// <summary>请求的新闻不存在（404）。</summary>
public sealed class NewsArticleNotFoundException(Guid id)
    : AxiomException(ApiErrorCodes.NEWS_NOT_FOUND, $"指定的新闻不存在：{id:N}。", id);

/// <summary>并发冲突——资源已被其他请求修改（409）。</summary>
public sealed class NewsArticleConcurrencyConflictException(Guid id)
    : AxiomException(ApiErrorCodes.NEWS_CONCURRENCY_CONFLICT, $"新闻已被其他请求修改，请重新获取后再操作。Id={id:N}", id);

/// <summary>缺少或格式不正确的 If-Match 头（428 / 400）。</summary>
public sealed class MissingOrInvalidETagException(string message)
    : AxiomException(ApiErrorCodes.REQUEST_INVALID_IF_MATCH, message);

/// <summary>领域不变量被破坏——字段为空、超长或翻译字段非法（400）。</summary>
public sealed class NewsArticleValidationException : AxiomException
{
    private NewsArticleValidationException(string field, string errorCode, string message, params object[] args)
        : base(errorCode, message, args)
    {
        Field = field;
    }

    /// <summary>触发校验失败的字段名，供接口层构造 ProblemDetails 时使用。</summary>
    public string Field { get; }

    public static NewsArticleValidationException Required(string field) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "该字段不能为空。", field);

    public static NewsArticleValidationException TooLong(string field, int maxLength) =>
        new(field, ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, $"长度不能超过 {maxLength} 个字符。", field, maxLength);

    /// <summary>翻译写入时字段不在白名单内。</summary>
    public static NewsArticleValidationException UnknownTranslatableField(string field) =>
        new(field, ApiErrorCodes.LOCALIZATION_UNSUPPORTED_FIELD, $"不支持翻译该字段：{field}。", field);
}
