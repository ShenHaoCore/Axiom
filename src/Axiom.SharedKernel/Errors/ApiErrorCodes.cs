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
    public const string REGION_NOT_FOUND = "ReferenceData.Region.NotFound";
    public const string REGION_UNIQUE_KEY_CONFLICT = "ReferenceData.Region.UniqueKeyConflict";
    public const string REGION_HAS_CHILDREN = "ReferenceData.Region.HasChildren";
    public const string REGION_COUNTRY_NOT_FOUND = "ReferenceData.Region.CountryNotFound";
    public const string REGION_INVALID_LEVEL = "ReferenceData.Region.Validation.InvalidLevel";
    public const string REGION_INVALID_COUNTRY_CODE = "ReferenceData.Region.Validation.InvalidCountryCode";
    public const string REGION_LATITUDE_OUT_OF_RANGE = "ReferenceData.Region.Validation.LatitudeOutOfRange";
    public const string REGION_LONGITUDE_OUT_OF_RANGE = "ReferenceData.Region.Validation.LongitudeOutOfRange";

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
