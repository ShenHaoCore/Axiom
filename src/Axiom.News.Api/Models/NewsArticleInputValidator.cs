// 校验新闻文章输入的长度和必填项。
namespace Axiom.News.Api.Models;

public static class NewsArticleInputValidator
{
    public const int MAX_TITLE_LENGTH = 200;
    public const int MAX_SUMMARY_LENGTH = 500;
    public const int MAX_CONTENT_LENGTH = 50000;
    public const int MAX_CATEGORY_LENGTH = 50;
    public const int MAX_AUTHOR_LENGTH = 100;
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(NewsArticleStatus.Draft),
        nameof(NewsArticleStatus.Published),
        nameof(NewsArticleStatus.Archived)
    };

    public static Dictionary<string, string[]> Validate(UpsertNewsArticleRequest request, bool isCreate)
    {
        var errors = new Dictionary<string, string[]>();
        if (isCreate)
        {
            ValidateRequired(errors, request.Title, nameof(request.Title), MAX_TITLE_LENGTH);
            ValidateRequired(errors, request.Content, nameof(request.Content), MAX_CONTENT_LENGTH);
            ValidateRequired(errors, request.Category, nameof(request.Category), MAX_CATEGORY_LENGTH);
            ValidateRequired(errors, request.Author, nameof(request.Author), MAX_AUTHOR_LENGTH);
        }
        else
        {
            ValidateOptional(errors, request.Title, nameof(request.Title), MAX_TITLE_LENGTH);
            ValidateOptional(errors, request.Content, nameof(request.Content), MAX_CONTENT_LENGTH);
            ValidateOptional(errors, request.Category, nameof(request.Category), MAX_CATEGORY_LENGTH);
            ValidateOptional(errors, request.Author, nameof(request.Author), MAX_AUTHOR_LENGTH);
        }

        if (request.Summary is not null && request.Summary.Length > MAX_SUMMARY_LENGTH)
        {
            errors[nameof(request.Summary)] = [$"摘要长度不能超过 {MAX_SUMMARY_LENGTH} 个字符。"];
        }

        if (request.Status is not null && !ValidStatuses.Contains(request.Status))
        {
            errors[nameof(request.Status)] = ["状态必须是 Draft、Published 或 Archived。"];
        }

        return errors;
    }

    private static void ValidateRequired(
        Dictionary<string, string[]> errors,
        string? value,
        string propertyName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[propertyName] = ["该字段不能为空。"];
            return;
        }

        if (value.Length > maxLength)
        {
            errors[propertyName] = [$"长度不能超过 {maxLength} 个字符。"];
        }
    }

    private static void ValidateOptional(
        Dictionary<string, string[]> errors,
        string? value,
        string propertyName,
        int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors[propertyName] = [$"长度不能超过 {maxLength} 个字符。"];
        }
    }
}
