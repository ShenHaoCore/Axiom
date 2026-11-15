// 枚举字典列表 / 表单模型。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed record ReferenceEnumView(
    Guid Id,
    string Category,
    string Code,
    string Name,
    int SortOrder);

public sealed class ReferenceEnumInput
{
    [Required(ErrorMessage = "Validation.Enum.CategoryRequired")]
    [StringLength(64, ErrorMessage = "Validation.Enum.CategoryTooLong")]
    [Display(Name = "Field.Enum.Category")]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Validation.Enum.CodeRequired")]
    [StringLength(64, ErrorMessage = "Validation.Enum.CodeTooLong")]
    [Display(Name = "Field.Enum.Code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Validation.Enum.NameRequired")]
    [StringLength(100, ErrorMessage = "Validation.Enum.NameTooLong")]
    [Display(Name = "Field.Enum.Name")]
    public string? Name { get; set; }

    [Display(Name = "Field.Enum.SortOrder")]
    public int SortOrder { get; set; }

    public static ReferenceEnumInput From(ReferenceEnumView? item) => item is null
        ? new ReferenceEnumInput()
        : new ReferenceEnumInput
        {
            Category = item.Category,
            Code = item.Code,
            Name = item.Name,
            SortOrder = item.SortOrder
        };
}
