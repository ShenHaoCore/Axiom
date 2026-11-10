// 承载并校验行政区新增、编辑页面的用户输入。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class RegionInput
{
    [Range(1, 3, ErrorMessage = "Validation.Region.LevelRange")]
    [Display(Name = "Field.Region.Level")]
    public int Level { get; set; } = 1;

    [Required(ErrorMessage = "Validation.Region.CountryCodeRequired")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Validation.Region.CountryCodeFormat")]
    [Display(Name = "Field.Region.CountryCode")]
    public string? CountryCode { get; set; }

    [StringLength(20, ErrorMessage = "Validation.Region.Admin1CodeTooLong")]
    [Display(Name = "Field.Region.Admin1Code")]
    public string? Admin1Code { get; set; }

    [StringLength(50, ErrorMessage = "Validation.Region.CodeTooLong")]
    [Display(Name = "Field.Region.Code")]
    public string? Code { get; set; }

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

    [Display(Name = "Field.Region.Parent")]
    public Guid? ParentId { get; set; }

    public static RegionInput From(RegionView? region) => region is null
        ? new RegionInput()
        : new RegionInput
        {
            Level = region.Level,
            CountryCode = region.CountryCode,
            Admin1Code = region.Admin1Code,
            Code = region.Code,
            Name = region.Name,
            AsciiName = region.AsciiName,
            LocalName = region.LocalName,
            Latitude = region.Latitude,
            Longitude = region.Longitude,
            Population = region.Population,
            ParentId = region.ParentId
        };
}
