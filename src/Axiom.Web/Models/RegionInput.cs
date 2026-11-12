// 承载并校验行政区新增、编辑页面的用户输入。
// 按 (country, code) 复合业务键标识；Code / Depth / Type 为业务标识，编辑页不可改。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class RegionInput
{
    [Range(0, 3, ErrorMessage = "Validation.Region.DepthRange")]
    [Display(Name = "Field.Region.Depth")]
    public int Depth { get; set; }

    [Required(ErrorMessage = "Validation.Region.CountryCodeRequired")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Validation.Region.CountryCodeFormat")]
    [Display(Name = "Field.Region.CountryCode")]
    public string? CountryCode { get; set; }

    [Required(ErrorMessage = "Validation.Region.CodeRequired")]
    [StringLength(50, ErrorMessage = "Validation.Region.CodeTooLong")]
    [Display(Name = "Field.Region.Code")]
    public string? Code { get; set; }

    [StringLength(50, ErrorMessage = "Validation.Region.ParentCodeTooLong")]
    [Display(Name = "Field.Region.Parent")]
    public string? ParentCode { get; set; }

    // 国别口径类型（province/municipality/autonomous_region/sar/prefecture_city/…）；留空由服务端按国家与层级推导。
    [StringLength(32, ErrorMessage = "Validation.Region.TypeTooLong")]
    [Display(Name = "Field.Region.Type")]
    public string? Type { get; set; }

    // ISO 3166-2 代码（如 CN-11）；可选。
    [StringLength(16, ErrorMessage = "Validation.Region.CodeIsoTooLong")]
    [Display(Name = "Field.Region.CodeIso")]
    public string? CodeIso { get; set; }

    [Required(ErrorMessage = "Validation.Region.NameRequired")]
    [StringLength(200, ErrorMessage = "Validation.Region.NameTooLong")]
    [Display(Name = "Field.Region.Name")]
    public string? Name { get; set; }

    [StringLength(200, ErrorMessage = "Validation.Region.AsciiNameTooLong")]
    [Display(Name = "Field.Region.AsciiName")]
    public string? AsciiName { get; set; }

    [StringLength(200, ErrorMessage = "Validation.Region.LocalNameTooLong")]
    [Display(Name = "Field.Region.LocalName")]
    public string? LocalName { get; set; }

    [Range(-90, 90, ErrorMessage = "Validation.Region.LatitudeRange")]
    [Display(Name = "Field.Region.Latitude")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Validation.Region.LongitudeRange")]
    [Display(Name = "Field.Region.Longitude")]
    public double? Longitude { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "Validation.Region.PopulationRange")]
    [Display(Name = "Field.Region.Population")]
    public long? Population { get; set; }

    public static RegionInput From(RegionView? region) => region is null
        ? new RegionInput()
        : new RegionInput
        {
            Depth = region.Depth,
            CountryCode = region.CountryCode,
            Code = region.Code,
            ParentCode = region.ParentCode,
            Type = region.Type,
            Name = region.Name,
            AsciiName = region.AsciiName,
            LocalName = region.LocalName,
            Latitude = region.Latitude,
            Longitude = region.Longitude,
            Population = region.Population,
            CodeIso = region.CodeIso
        };
}
