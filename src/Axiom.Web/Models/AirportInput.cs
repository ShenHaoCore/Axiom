// 承载并校验新增机场页面的用户输入。
// ErrorMessage 与 Display(Name) 都使用 SharedResource 的资源键，由 DataAnnotations 本地化。
using System.ComponentModel.DataAnnotations;

namespace Axiom.Web.Models;

public sealed class AirportInput
{
    [Required(ErrorMessage = "Validation.Airport.CodeRequired")]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Validation.Airport.CodeFormat")]
    [Display(Name = "Field.Airport.Code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Validation.Airport.NameRequired")]
    [StringLength(200, ErrorMessage = "Validation.Airport.NameTooLong")]
    [Display(Name = "Field.Airport.Name")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Validation.Airport.CountryCodeRequired")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Validation.Airport.CountryCodeFormat")]
    [Display(Name = "Field.Airport.CountryCode")]
    public string? CountryCode { get; set; }

    [StringLength(100, ErrorMessage = "Validation.Airport.CityTooLong")]
    [Display(Name = "Field.Airport.City")]
    public string? City { get; set; }

    [Range(-90, 90, ErrorMessage = "Validation.Airport.LatitudeRange")]
    [Display(Name = "Field.Airport.Latitude")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Validation.Airport.LongitudeRange")]
    [Display(Name = "Field.Airport.Longitude")]
    public double? Longitude { get; set; }
}
