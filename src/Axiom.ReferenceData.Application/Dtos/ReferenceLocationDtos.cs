// Application 层 DTO：地点档案。
using Axiom.ReferenceData.Domain.Entities;
using Axiom.SharedKernel.Errors;

namespace Axiom.ReferenceData.Application.Dtos;

public sealed record ReferenceLocationDto(
    Guid Id,
    Guid ConcurrencyToken,
    string Type,
    string Code,
    string Name,
    string CountryCode,
    string? City,
    double? Latitude,
    double? Longitude);

public sealed record UpsertReferenceLocationInput(
    string Code,
    string Name,
    string CountryCode,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ReferenceLocationListQuery(
    string? Code = null,
    string? Name = null,
    int Page = 1,
    int PageSize = 20);

// 输入校验（按 ABP 约定：校验逻辑归 Application 层 DTO）。
// 只产出错误码与参数，具体文案由接口层按语言渲染。
public static class ReferenceLocationInputValidator
{
    public static Dictionary<string, ValidationError[]> Validate(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input,
        bool isCreate)
    {
        var errors = new Dictionary<string, ValidationError[]>();

        var code = input.Code?.Trim() ?? string.Empty;
        if (!ReferenceLocation.IsValidCode(type, code))
        {
            errors["code"] = [new ValidationError(ReferenceLocation.GetCodeFormatErrorCode(type))];
        }

        var name = input.Name?.Trim() ?? string.Empty;
        if (isCreate && string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = [new ValidationError(ApiErrorCodes.VALIDATION_FIELD_REQUIRED, "name")];
        }
        else if (name.Length > ReferenceLocation.MaxNameLength)
        {
            errors["name"] = [new ValidationError(
                ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, "name", ReferenceLocation.MaxNameLength)];
        }

        var countryCode = input.CountryCode?.Trim() ?? string.Empty;
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
        {
            errors["countryCode"] = [new ValidationError(ApiErrorCodes.LOCATION_INVALID_COUNTRY_CODE)];
        }

        if (input.City is { Length: > ReferenceLocation.MaxCityLength })
        {
            errors["city"] = [new ValidationError(
                ApiErrorCodes.VALIDATION_FIELD_TOO_LONG, "city", ReferenceLocation.MaxCityLength)];
        }

        if (input.Latitude is < -90 or > 90)
        {
            errors["latitude"] = [new ValidationError(ApiErrorCodes.LOCATION_LATITUDE_OUT_OF_RANGE)];
        }

        if (input.Longitude is < -180 or > 180)
        {
            errors["longitude"] = [new ValidationError(ApiErrorCodes.LOCATION_LONGITUDE_OUT_OF_RANGE)];
        }

        return errors;
    }
}
