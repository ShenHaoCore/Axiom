// 统一错误码常量：同时作为资源键，供各 API 用 IStringLocalizer 输出本地化消息。
// 约定：模块前缀.资源.错误类型；值一旦对外发布就视为契约，不要随意改名。
namespace Axiom.SharedKernel.Errors;

public static class ApiErrorCodes
{
    // ---------- 通用 ----------
    public const string REQUEST_BODY_REQUIRED = "Request.BodyRequired";
    public const string REQUEST_MISSING_IF_MATCH = "Request.MissingIfMatch";
    public const string REQUEST_INVALID_IF_MATCH = "Request.InvalidIfMatch";

    public const string VALIDATION_FAILED = "Validation.Failed";
    public const string VALIDATION_FIELD_REQUIRED = "Validation.FieldRequired";
    public const string VALIDATION_FIELD_TOO_LONG = "Validation.FieldTooLong";
    public const string VALIDATION_PAGE_OUT_OF_RANGE = "Validation.PageOutOfRange";
    public const string VALIDATION_PAGE_SIZE_OUT_OF_RANGE = "Validation.PageSizeOutOfRange";

    public const string LOCALIZATION_INVALID_CULTURE = "Localization.InvalidCulture";
    public const string LOCALIZATION_UNSUPPORTED_CULTURE = "Localization.UnsupportedCulture";
    public const string LOCALIZATION_UNSUPPORTED_FIELD = "Localization.UnsupportedField";

    public const string SERVER_INTERNAL_ERROR = "Server.InternalError";

    // ---------- 网关 ----------
    public const string GATEWAY_TIMEOUT = "Gateway.Timeout";
    public const string GATEWAY_UNAVAILABLE = "Gateway.Unavailable";

    // ---------- 新闻 ----------
    public const string NEWS_NOT_FOUND = "News.Article.NotFound";
    public const string NEWS_CONCURRENCY_CONFLICT = "News.Article.ConcurrencyConflict";
    public const string NEWS_INVALID_STATUS = "News.Validation.InvalidStatus";

    // ---------- 行政区 ----------
    public const string GEO_DIVISION_NOT_FOUND = "ReferenceData.GeoDivision.NotFound";
    public const string GEO_DIVISION_UNIQUE_KEY_CONFLICT = "ReferenceData.GeoDivision.UniqueKeyConflict";
    public const string GEO_DIVISION_HAS_CHILDREN = "ReferenceData.GeoDivision.HasChildren";
    public const string GEO_DIVISION_COUNTRY_NOT_FOUND = "ReferenceData.GeoDivision.CountryNotFound";
    public const string GEO_DIVISION_INVALID_LEVEL = "ReferenceData.GeoDivision.Validation.InvalidLevel";
    public const string GEO_DIVISION_INVALID_KIND = "ReferenceData.GeoDivision.Validation.InvalidKind";
    public const string GEO_DIVISION_INVALID_COUNTRY_CODE = "ReferenceData.GeoDivision.Validation.InvalidCountryCode";
    public const string GEO_DIVISION_LATITUDE_OUT_OF_RANGE = "ReferenceData.GeoDivision.Validation.LatitudeOutOfRange";
    public const string GEO_DIVISION_LONGITUDE_OUT_OF_RANGE = "ReferenceData.GeoDivision.Validation.LongitudeOutOfRange";

    // ---------- 枚举字典 ----------
    public const string REFERENCE_ENUM_NOT_FOUND = "ReferenceData.Enum.NotFound";
    public const string REFERENCE_ENUM_CODE_CONFLICT = "ReferenceData.Enum.CodeConflict";
    public const string REFERENCE_ENUM_IN_USE = "ReferenceData.Enum.InUse";

    // ---------- 地点档案 ----------
    public const string LOCATION_NOT_FOUND = "ReferenceData.Location.NotFound";
    public const string LOCATION_CODE_CONFLICT = "ReferenceData.Location.CodeConflict";
    public const string LOCATION_CONCURRENCY_CONFLICT = "ReferenceData.Location.ConcurrencyConflict";
    public const string LOCATION_INVALID_COUNTRY_CODE = "ReferenceData.Location.Validation.InvalidCountryCode";
    public const string LOCATION_LATITUDE_OUT_OF_RANGE = "ReferenceData.Location.Validation.LatitudeOutOfRange";
    public const string LOCATION_LONGITUDE_OUT_OF_RANGE = "ReferenceData.Location.Validation.LongitudeOutOfRange";
    public const string LOCATION_AIRPORT_CODE_FORMAT = "ReferenceData.Location.Validation.AirportCodeFormat";
    public const string LOCATION_SEAPORT_CODE_FORMAT = "ReferenceData.Location.Validation.SeaportCodeFormat";
    public const string LOCATION_RAIL_STATION_CODE_FORMAT = "ReferenceData.Location.Validation.RailStationCodeFormat";
    public const string LOCATION_UNSUPPORTED_TYPE = "ReferenceData.Location.Validation.UnsupportedType";
}
