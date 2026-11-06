// Application 层 DTO：请求 / 响应 / 校验。
// Domain 层只持有 Entity；DTO 用于 HTTP 端点与 AppService 之间的数据传递。
using Axiom.News.Domain.Constants;
using Axiom.SharedKernel.Errors;

namespace Axiom.News.Application.Dtos;

public sealed record NewsArticleDto(
    Guid Id,
    string Title,
    string? Summary,
    string Content,
    string Category,
    string Status,
    string Author,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyToken);

public sealed record CreateNewsArticleInput(
    string Title,
    string? Summary,
    string Content,
    string Category,
    string Author,
    string? Status);

public sealed record UpdateNewsArticleInput(
    string? Title,
    string? Summary,
    string? Content,
    string? Category,
    string? Author,
    string? Status);

public sealed record NewsArticleListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Category = null,
    string? Status = null,
    string? Keyword = null);

/// <summary>某个实体在单一语言下的翻译集合（字段名 → 译文）。</summary>
public sealed record TranslationDto(string Culture, IReadOnlyDictionary<string, string> Values);

/// <summary>整语言翻译的写入体：未出现在 Values 中的字段会被删除。</summary>
public sealed record SaveTranslationsInput(IReadOnlyDictionary<string, string> Values);

// 输入校验——放在 Application 层，因为它消费 Domain 层的长度常量。
// 只产出错误码与参数，具体文案由接口层用资源文件本地化。
public static class NewsArticleInputValidator
{
    public static Dictionary<string, ValidationError[]> ValidateCreate(CreateNewsArticleInput request)
    {
        var errors = new Dictionary<string, ValidationError[]>();
        ValidateRequired(errors, request.Title, nameof(request.Title), NewsArticleLengths.MaxTitleLength);
        ValidateRequired(errors, request.Content, nameof(request.Content), NewsArticleLengths.MaxContentLength);
        ValidateRequired(errors, request.Category, nameof(request.Category), NewsArticleLengths.MaxCategoryLength);
        ValidateRequired(errors, request.Author, nameof(request.Author), NewsArticleLengths.MaxAuthorLength);
        ValidateOptional(errors, request.Summary, nameof(request.Summary), NewsArticleLengths.MaxSummaryLength);
        ValidateStatus(errors, request.Status);
        return errors;
    }

    public static Dictionary<string, ValidationError[]> ValidateUpdate(UpdateNewsArticleInput request)
    {
        var errors = new Dictionary<string, ValidationError[]>();
        ValidateOptional(errors, request.Title, nameof(request.Title), NewsArticleLengths.MaxTitleLength);
        ValidateOptional(errors, request.Content, nameof(request.Content), NewsArticleLengths.MaxContentLength);
        ValidateOptional(errors, request.Category, nameof(request.Category), NewsArticleLengths.MaxCategoryLength);
        ValidateOptional(errors, request.Author, nameof(request.Author), NewsArticleLengths.MaxAuthorLength);
        ValidateOptional(errors, request.Summary, nameof(request.Summary), NewsArticleLengths.MaxSummaryLength);
        ValidateStatus(errors, request.Status);
        return errors;
    }

    private static void ValidateRequired(
        Dictionary<string, ValidationError[]> errors,
        string? value,
        string propertyName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[propertyName] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_REQUIRED, propertyName)];
            return;
        }
        if (value.Length > maxLength)
        {
            errors[propertyName] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, propertyName, maxLength)];
        }
    }

    private static void ValidateOptional(
        Dictionary<string, ValidationError[]> errors,
        string? value,
        string propertyName,
        int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors[propertyName] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, propertyName, maxLength)];
        }
    }

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Published", "Archived"
    };

    private static void ValidateStatus(Dictionary<string, ValidationError[]> errors, string? status)
    {
        if (status is not null && !ValidStatuses.Contains(status))
        {
            errors[nameof(status)] = [new ValidationError(ApiErrorCodes.NEWS_INVALID_STATUS)];
        }
    }
}
