// 承载并校验新增机场页面的用户输入。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class AirportInput
{
    [Required(ErrorMessage = "机场代码必填。")]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "机场代码必须是 3 个英文字母。")]
    [Display(Name = "机场代码")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "机场名称必填。")]
    [StringLength(200, ErrorMessage = "机场名称不能超过 200 个字符。")]
    [Display(Name = "机场名称")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "国家代码必填。")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "国家代码必须是 2 个英文字母。")]
    [Display(Name = "国家代码")]
    public string? CountryCode { get; set; }

    [StringLength(100, ErrorMessage = "城市名称不能超过 100 个字符。")]
    [Display(Name = "城市")]
    public string? City { get; set; }

    [Range(-90, 90, ErrorMessage = "纬度必须位于 -90 到 90 之间。")]
    [Display(Name = "纬度")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "经度必须位于 -180 到 180 之间。")]
    [Display(Name = "经度")]
    public double? Longitude { get; set; }
}
