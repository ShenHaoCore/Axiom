// 承载并校验新闻编辑页面的用户输入。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web;

public sealed class NewsInput
{
    [Required(ErrorMessage = "标题必填。")]
    [StringLength(200, ErrorMessage = "标题不能超过 200 个字符。")]
    [Display(Name = "标题")]
    public string? Title { get; set; }

    [StringLength(500, ErrorMessage = "摘要不能超过 500 个字符。")]
    [Display(Name = "摘要")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "正文必填。")]
    [StringLength(50000, ErrorMessage = "正文不能超过 50000 个字符。")]
    [Display(Name = "正文")]
    public string? Content { get; set; }

    [Required(ErrorMessage = "分类必填。")]
    [StringLength(50, ErrorMessage = "分类不能超过 50 个字符。")]
    [Display(Name = "分类")]
    public string? Category { get; set; }

    [Required(ErrorMessage = "作者必填。")]
    [StringLength(100, ErrorMessage = "作者不能超过 100 个字符。")]
    [Display(Name = "作者")]
    public string? Author { get; set; }

    [RegularExpression("^(Draft|Published|Archived)$", ErrorMessage = "状态必须为 Draft、Published 或 Archived。")]
    [Display(Name = "状态")]
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
