// 承载并校验新闻编辑页面的用户输入。
// ErrorMessage 与 Display(Name) 都使用 SharedResource 的资源键，由 DataAnnotations 本地化。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class NewsInput
{
    [Required(ErrorMessage = "Validation.News.TitleRequired")]
    [StringLength(200, ErrorMessage = "Validation.News.TitleTooLong")]
    [Display(Name = "Field.News.Title")]
    public string? Title { get; set; }

    [StringLength(500, ErrorMessage = "Validation.News.SummaryTooLong")]
    [Display(Name = "Field.News.Summary")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Validation.News.ContentRequired")]
    [StringLength(50000, ErrorMessage = "Validation.News.ContentTooLong")]
    [Display(Name = "Field.News.Content")]
    public string? Content { get; set; }

    [Required(ErrorMessage = "Validation.News.CategoryRequired")]
    [StringLength(50, ErrorMessage = "Validation.News.CategoryTooLong")]
    [Display(Name = "Field.News.Category")]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Validation.News.AuthorRequired")]
    [StringLength(100, ErrorMessage = "Validation.News.AuthorTooLong")]
    [Display(Name = "Field.News.Author")]
    public string? Author { get; set; }

    [RegularExpression("^(Draft|Published|Archived)$", ErrorMessage = "Validation.News.StatusInvalid")]
    [Display(Name = "Field.News.Status")]
    public string Status { get; set; } = "Draft";

    public static NewsInput From(NewsArticleView? article) => article is null
        ? new NewsInput()
        : new NewsInput
        {
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            Category = article.Category,
            Author = article.Author,
            Status = article.Status
        };
}
