// 承载并校验行政区新增、编辑页面的用户输入。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class GeoDivisionInput
{
    [Range(1, 16, ErrorMessage = "Validation.GeoDivision.LevelRange")]
    [Display(Name = "Field.GeoDivision.Level")]
    public int Level { get; set; } = 1;

    [StringLength(64, ErrorMessage = "Validation.GeoDivision.KindTooLong")]
    [Display(Name = "Field.GeoDivision.Kind")]
    public string Kind { get; set; } = "standard";

    [Required(ErrorMessage = "Validation.GeoDivision.CountryCodeRequired")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Validation.GeoDivision.CountryCodeFormat")]
    [Display(Name = "Field.GeoDivision.CountryCode")]
    public string? CountryCode { get; set; }

    [StringLength(20, ErrorMessage = "Validation.GeoDivision.SubdivisionCodeTooLong")]
    [Display(Name = "Field.GeoDivision.SubdivisionCode")]
    public string? SubdivisionCode { get; set; }

    [StringLength(50, ErrorMessage = "Validation.GeoDivision.CodeTooLong")]
    [Display(Name = "Field.GeoDivision.Code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Validation.GeoDivision.NameRequired")]
    [StringLength(200, ErrorMessage = "Validation.GeoDivision.NameTooLong")]
    [Display(Name = "Field.GeoDivision.Name")]
    public string? Name { get; set; }

    [StringLength(200, ErrorMessage = "Validation.GeoDivision.AsciiNameTooLong")]
    [Display(Name = "Field.GeoDivision.AsciiName")]
    public string? AsciiName { get; set; }

    [StringLength(200, ErrorMessage = "Validation.GeoDivision.LocalNameTooLong")]
    [Display(Name = "Field.GeoDivision.LocalName")]
    public string? LocalName { get; set; }

    [Range(-90, 90, ErrorMessage = "Validation.GeoDivision.LatitudeRange")]
    [Display(Name = "Field.GeoDivision.Latitude")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Validation.GeoDivision.LongitudeRange")]
    [Display(Name = "Field.GeoDivision.Longitude")]
    public double? Longitude { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "Validation.GeoDivision.PopulationRange")]
    [Display(Name = "Field.GeoDivision.Population")]
    public long? Population { get; set; }

    [Display(Name = "Field.GeoDivision.Parent")]
    public Guid? ParentId { get; set; }

    public static GeoDivisionInput From(GeoDivisionView? region) => region is null
        ? new GeoDivisionInput()
        : new GeoDivisionInput
        {
            Level = region.Level,
            Kind = region.Kind,
            CountryCode = region.CountryCode,
            SubdivisionCode = region.SubdivisionCode,
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
