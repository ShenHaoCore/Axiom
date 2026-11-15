// 为 Web 集成测试替换用户认证和网关传输边界。
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Axiom.IntegrationTests;

public sealed class WebFactory : WebApplicationFactory<WebApplicationMarker>
{
    internal static readonly Guid AirportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid GeoDivisionCountryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid GeoDivisionAdmin1Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid GeoDivisionCityId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SCHEME;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SCHEME, _ => { });
            // 测试环境没有 AuthHub，用静态发现文档替换元数据获取，避免挑战阶段发起真实 HTTP 请求。
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new StaticOidcConfigurationManager());
            services.AddHttpClient<ReferenceDataClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new GatewayStubHandler());
            services.AddHttpClient<GeoDivisionClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new GatewayStubHandler());
            services.AddHttpClient<ReferenceEnumClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new GatewayStubHandler());
        });
    }

    private sealed class GatewayStubHandler : HttpMessageHandler
    {
        private const string ManageToken = "test-access-token";
        private const string ReadToken = "test-read-access-token";
        private readonly Dictionary<Guid, AirportState> _airports = new();
        private readonly Dictionary<Guid, GeoDivisionView> _geoDivisions = new();

        public GatewayStubHandler()
        {
            _airports.Add(AirportId, new AirportState(
                new AirportResponse(AirportId, "SHA", "上海浦东国际机场", "CN", "上海", 31.1443, 121.8083),
                1));
            _geoDivisions.Add(GeoDivisionCountryId, new GeoDivisionView(
                GeoDivisionCountryId, null, 1, "country", "CN", null, "CN", "中国", "China", "中国", null, null, 1400000000, null));
            _geoDivisions.Add(GeoDivisionAdmin1Id, new GeoDivisionView(
                GeoDivisionAdmin1Id, GeoDivisionCountryId, 2, "province", "CN", "JS", "JS", "江苏", "Jiangsu", "江苏", null, null, 85000000, null));
            _geoDivisions.Add(GeoDivisionCityId, new GeoDivisionView(
                GeoDivisionCityId, GeoDivisionAdmin1Id, 3, "prefecture_city", "CN", "JS", "SZ", "苏州", "Suzhou", "苏州", 31.3, 120.6, 12000000, null));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter;
            if (request.Headers.Authorization?.Scheme != "Bearer" ||
                token is not (ManageToken or ReadToken))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var canManage = token == ManageToken;
            var path = request.RequestUri?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (path == "/api/reference-data/airports")
            {
                return await AirportCollectionAsync(request, canManage, cancellationToken);
            }

            if (path.StartsWith("/api/reference-data/airports/", StringComparison.Ordinal))
            {
                return await AirportItemAsync(request, path, canManage, cancellationToken);
            }

            if (path == "/api/reference-data/enums" || path.StartsWith("/api/reference-data/enums?", StringComparison.Ordinal))
            {
                return EnumCollection(request);
            }

            if (path.StartsWith("/api/reference-data/enums/", StringComparison.Ordinal))
            {
                return EnumItem(request, path);
            }

            if (path == "/api/geo-divisions")
            {
                return await RegionCollectionAsync(request, canManage, cancellationToken);
            }

            if (path == "/api/geo-divisions/tree")
            {
                return RegionTree(request);
            }

            if (path.StartsWith("/api/geo-divisions/", StringComparison.Ordinal)
                && path.EndsWith("/children", StringComparison.OrdinalIgnoreCase)
                && request.Method == HttpMethod.Get)
            {
                return RegionChildren(request, path);
            }

            if (path.StartsWith("/api/geo-divisions/", StringComparison.Ordinal))
            {
                return await RegionItemAsync(request, path, canManage, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static readonly IReadOnlyList<ReferenceEnumView> KindEnums =
        [
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "geo_division.kind", "country", "国家", 1),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), "geo_division.kind", "province", "省", 2),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), "geo_division.kind", "municipality", "直辖市", 3),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"), "geo_division.kind", "autonomous_region", "自治区", 4),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"), "geo_division.kind", "special_administrative_region", "特别行政区", 5),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006"), "geo_division.kind", "autonomous_prefecture", "自治州", 6),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007"), "geo_division.kind", "prefecture_city", "地级市", 7),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000008"), "geo_division.kind", "prefecture", "地区", 8),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), "geo_division.kind", "league", "盟", 9),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000010"), "geo_division.kind", "county_level", "省直辖县级行政区划", 10),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000011"), "geo_division.kind", "county", "县", 11),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000012"), "geo_division.kind", "county_city", "县级市", 12),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000013"), "geo_division.kind", "district", "市辖区", 13),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000014"), "geo_division.kind", "autonomous_county", "自治县", 14),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000015"), "geo_division.kind", "banner", "旗", 15),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000016"), "geo_division.kind", "autonomous_banner", "自治旗", 16),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000017"), "geo_division.kind", "forestry_area", "林区", 17),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000018"), "geo_division.kind", "special_zone", "特区", 18),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000019"), "geo_division.kind", "other", "其他", 19),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000020"), "geo_division.kind", "state", "州", 20),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000021"), "geo_division.kind", "borough", "自治市镇", 21),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000022"), "geo_division.kind", "parish", "教区", 22),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000023"), "geo_division.kind", "census_area", "人口普查区", 23),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000024"), "geo_division.kind", "city", "市", 24),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000025"), "geo_division.kind", "constituent_country", "构成国", 25),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000026"), "geo_division.kind", "metropolis", "都", 26),
            new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000027"), "geo_division.kind", "special_ward", "特别区", 27)
        ];

        private HttpResponseMessage AirportCollection()
        {
            var items = _airports.Values.Select(state => state.Airport).OrderBy(airport => airport.Code).ToList();
            return Json(HttpStatusCode.OK, new PagedResponse<AirportResponse>(items, items.Count, 1, 20));
        }

        private static HttpResponseMessage EnumCollection(HttpRequestMessage request)
        {
            if (request.Method != HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var culture = ResolveCulture(request);
            var category = query.TryGetValue("category", out var categoryValue)
                ? categoryValue.ToString()
                : null;
            var items = string.IsNullOrWhiteSpace(category)
                ? KindEnums
                : KindEnums.Where(item => item.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            var localized = items.Select(item => LocalizeEnum(item, culture)).ToList();
            return Json(HttpStatusCode.OK, new PagedResponse<ReferenceEnumView>(localized, localized.Count, 1, Math.Max(localized.Count, 1)));
        }

        private static HttpResponseMessage EnumItem(HttpRequestMessage request, string path)
        {
            var rest = path["/api/reference-data/enums/".Length..].Trim('/');
            var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !Guid.TryParse(parts[0], out var id))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var item = KindEnums.FirstOrDefault(entry => entry.Id == id);
            if (item is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            // 译文子资源：测试桩按 Code 提供英文 Name。
            if (parts.Length >= 3 && parts[1].Equals("translations", StringComparison.OrdinalIgnoreCase))
            {
                var culture = parts[2];
                var values = string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)
                    && EnumEnglishNames.TryGetValue(item.Code, out var english)
                    ? new Dictionary<string, string> { ["Name"] = english }
                    : new Dictionary<string, string>();
                return Json(HttpStatusCode.OK, new TranslationView(culture, values));
            }

            return Json(HttpStatusCode.OK, LocalizeEnum(item, ResolveCulture(request)));
        }

        private static readonly Dictionary<string, string> EnumEnglishNames = new(StringComparer.Ordinal)
        {
            ["country"] = "Country",
            ["province"] = "Province",
            ["municipality"] = "Municipality",
            ["autonomous_region"] = "Autonomous Region",
            ["special_administrative_region"] = "Special Administrative Region",
            ["autonomous_prefecture"] = "Autonomous Prefecture",
            ["prefecture_city"] = "Prefecture-level City",
            ["prefecture"] = "Prefecture",
            ["league"] = "League",
            ["county_level"] = "Province-administered County-level Division",
            ["county"] = "County",
            ["county_city"] = "County-level City",
            ["district"] = "District",
            ["autonomous_county"] = "Autonomous County",
            ["banner"] = "Banner",
            ["autonomous_banner"] = "Autonomous Banner",
            ["forestry_area"] = "Forestry Area",
            ["special_zone"] = "Special Zone",
            ["state"] = "State",
            ["borough"] = "Borough",
            ["parish"] = "Parish",
            ["census_area"] = "Census Area",
            ["city"] = "City",
            ["constituent_country"] = "Constituent Country",
            ["metropolis"] = "Metropolis",
            ["special_ward"] = "Special Ward",
            ["other"] = "Other"
        };

        private static ReferenceEnumView LocalizeEnum(ReferenceEnumView item, string culture) =>
            string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)
            && EnumEnglishNames.TryGetValue(item.Code, out var english)
                ? item with { Name = english }
                : item;

        private async Task<HttpResponseMessage> AirportCollectionAsync(
            HttpRequestMessage request,
            bool canManage,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return AirportCollection();
            }

            if (request.Method != HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            var input = await request.Content!.ReadFromJsonAsync<AirportInput>(cancellationToken);
            if (input?.Code is null || input.Name is null || input.CountryCode is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var code = input.Code.ToUpperInvariant();
            if (_airports.Values.Any(state => state.Airport.Code == code))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            var id = Guid.NewGuid();
            var airport = new AirportResponse(
                id, code, input.Name, input.CountryCode.ToUpperInvariant(), input.City, input.Latitude, input.Longitude);
            _airports.Add(id, new AirportState(airport, 1));
            var response = Json(HttpStatusCode.Created, airport, AirportETag(id));
            response.Headers.Location = new Uri($"/api/reference-data/airports/{id}", UriKind.Relative);
            return response;
        }

        private async Task<HttpResponseMessage> AirportItemAsync(
            HttpRequestMessage request,
            string path,
            bool canManage,
            CancellationToken cancellationToken)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (!Guid.TryParse(segments[3], out var id) || !_airports.TryGetValue(id, out var state))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (segments.Length > 4 && segments[4] == "translations")
            {
                var culture = segments.Length > 5 ? segments[5] : "en";
                return Json(HttpStatusCode.OK, new TranslationView(culture, new Dictionary<string, string>()));
            }

            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, state.Airport, AirportETag(id));
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (!request.Headers.TryGetValues("If-Match", out var values))
            {
                return new HttpResponseMessage((HttpStatusCode)428);
            }

            if (values.SingleOrDefault() != AirportETag(id))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            if (request.Method == HttpMethod.Delete)
            {
                _airports.Remove(id);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method != HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var input = await request.Content!.ReadFromJsonAsync<AirportInput>(cancellationToken);
            if (input?.Code is null || input.Name is null || input.CountryCode is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var updated = new AirportResponse(
                id,
                input.Code.ToUpperInvariant(),
                input.Name,
                input.CountryCode.ToUpperInvariant(),
                input.City,
                input.Latitude,
                input.Longitude);
            _airports[id] = state with { Airport = updated, Version = state.Version + 1 };
            return Json(HttpStatusCode.OK, updated, AirportETag(id));
        }

        private async Task<HttpResponseMessage> RegionCollectionAsync(
            HttpRequestMessage request,
            bool canManage,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
                var culture = ResolveCulture(request);
                IEnumerable<GeoDivisionView> regions = _geoDivisions.Values;
                if (query.TryGetValue("level", out var levelValue) && int.TryParse(levelValue.ToString(), out var parsedLevel))
                {
                    regions = regions.Where(region => region.Level == parsedLevel);
                }

                if (query.TryGetValue("countryCode", out var countryCode) && !string.IsNullOrWhiteSpace(countryCode))
                {
                    regions = regions.Where(region => region.CountryCode == countryCode.ToString().ToUpperInvariant());
                }

                if (query.TryGetValue("subdivisionCode", out var subdivisionCode) && !string.IsNullOrWhiteSpace(subdivisionCode))
                {
                    regions = regions.Where(region => region.SubdivisionCode == subdivisionCode.ToString());
                }

                if (query.TryGetValue("q", out var q) && !string.IsNullOrWhiteSpace(q))
                {
                    regions = regions.Where(region => region.Name.Contains(q.ToString(), StringComparison.OrdinalIgnoreCase));
                }

                var page = query.TryGetValue("page", out var pageValue) && int.TryParse(pageValue, out var parsedPage)
                    ? Math.Max(1, parsedPage)
                    : 1;
                var pageSize = query.TryGetValue("pageSize", out var sizeValue) && int.TryParse(sizeValue, out var parsedSize)
                    ? Math.Clamp(parsedSize, 1, 200)
                    : 50;
                var ordered = regions.OrderBy(region => region.Code).ToList();
                var items = ordered.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(region => Localize(region, culture))
                    .ToList();
                return Json(HttpStatusCode.OK, new PagedResponse<GeoDivisionView>(items, ordered.Count, page, pageSize));
            }

            if (request.Method != HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            var input = await request.Content!.ReadFromJsonAsync<GeoDivisionCreatePayload>(cancellationToken);
            if (input?.CountryCode is null || input.Name is null || input.Level < 1)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var normalizedCountry = input.CountryCode.ToUpperInvariant();
            var code = (input.Code ?? input.Name).ToUpperInvariant();
            if (_geoDivisions.Values.Any(region =>
                    region.CountryCode == normalizedCountry && region.Level == input.Level && region.Code == code))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            var id = Guid.NewGuid();
            var region = new GeoDivisionView(
                id,
                input.Level == 1 ? null : input.ParentId,
                input.Level,
                string.IsNullOrWhiteSpace(input.Kind) ? (input.Level == 1 ? "country" : "province") : input.Kind.Trim().ToLowerInvariant(),
                normalizedCountry,
                input.SubdivisionCode,
                code,
                input.Name,
                input.AsciiName,
                input.LocalName,
                input.Latitude,
                input.Longitude,
                input.Population,
                null);
            _geoDivisions.Add(id, region);
            var response = Json(HttpStatusCode.Created, region);
            response.Headers.Location = new Uri($"/api/geo-divisions/{id}", UriKind.Relative);
            return response;
        }

        private HttpResponseMessage RegionTree(HttpRequestMessage request)
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var countryCode = query["countryCode"].ToString().ToUpperInvariant();
            var country = _geoDivisions.Values.FirstOrDefault(region => region.Level == 1 && region.CountryCode == countryCode);
            return country is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json(HttpStatusCode.OK, Localize(BuildTree(country), ResolveCulture(request)));
        }

        private GeoDivisionTreeView BuildTree(GeoDivisionView region) => new(
            region.Id,
            region.Level,
            region.Kind,
            region.CountryCode,
            region.SubdivisionCode,
            region.Code,
            region.Name,
            region.Population,
            _geoDivisions.Values
                .Where(child => child.ParentId == region.Id)
                .OrderBy(child => child.Code)
                .Select(BuildTree)
                .ToList());

        private HttpResponseMessage RegionChildren(HttpRequestMessage request, string path)
        {
            var idText = path["/api/geo-divisions/".Length..];
            if (idText.EndsWith("/children", StringComparison.OrdinalIgnoreCase))
            {
                idText = idText[..^"/children".Length];
            }

            if (!Guid.TryParse(idText.Trim('/'), out var parentId) || !_geoDivisions.ContainsKey(parentId))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var culture = ResolveCulture(request);
            var children = _geoDivisions.Values
                .Where(region => region.ParentId == parentId)
                .OrderBy(region => region.Code)
                .Select(child =>
                {
                    var grandchildren = _geoDivisions.Values.Where(g => g.ParentId == child.Id).ToList();
                    var sameNameChildCount = grandchildren.Count(g =>
                        string.Equals(g.Name.Trim(), child.Name.Trim(), StringComparison.Ordinal));
                    return new GeoDivisionChildView(
                        child.Id,
                        child.Level,
                        child.Kind,
                        child.CountryCode,
                        child.SubdivisionCode,
                        child.Code,
                        Localize(child, culture).Name,
                        child.Population,
                        grandchildren.Count,
                        sameNameChildCount);
                })
                .ToList();
            return Json(HttpStatusCode.OK, children);
        }

        private async Task<HttpResponseMessage> RegionItemAsync(
            HttpRequestMessage request,
            string path,
            bool canManage,
            CancellationToken cancellationToken)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (!Guid.TryParse(segments[2], out var id) || !_geoDivisions.TryGetValue(id, out var region))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (segments.Length > 3 && segments[3] == "translations")
            {
                var culture = segments.Length > 4 ? segments[4] : "en";
                return Json(HttpStatusCode.OK, new TranslationView(culture, new Dictionary<string, string>()));
            }

            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, Localize(region, ResolveCulture(request)));
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (request.Method == HttpMethod.Delete)
            {
                if (_geoDivisions.Values.Any(child => child.ParentId == id))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                _geoDivisions.Remove(id);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method != HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var input = await request.Content!.ReadFromJsonAsync<GeoDivisionUpdatePayload>(cancellationToken);
            if (input is null || input.ParentId == id)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var code = string.IsNullOrWhiteSpace(input.Code) ? region.Code : input.Code.ToUpperInvariant();
            if (_geoDivisions.Values.Any(other =>
                    other.Id != id && other.CountryCode == region.CountryCode &&
                    other.Level == region.Level && other.Code == code))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            var updated = region with
            {
                Name = string.IsNullOrWhiteSpace(input.Name) ? region.Name : input.Name.Trim(),
                AsciiName = string.IsNullOrWhiteSpace(input.AsciiName) ? region.AsciiName : input.AsciiName.Trim(),
                LocalName = string.IsNullOrWhiteSpace(input.LocalName) ? region.LocalName : input.LocalName.Trim(),
                Code = code,
                Latitude = input.Latitude ?? region.Latitude,
                Longitude = input.Longitude ?? region.Longitude,
                Population = input.Population ?? region.Population,
                ParentId = input.ParentId ?? region.ParentId,
                Kind = string.IsNullOrWhiteSpace(input.Kind) ? region.Kind : input.Kind.Trim().ToLowerInvariant()
            };
            _geoDivisions[id] = updated;
            return Json(HttpStatusCode.OK, updated);
        }

        // 测试桩：按 Accept-Language 用 AsciiName 模拟英文译文（与 Mock 行为对齐）。
        private static string ResolveCulture(HttpRequestMessage request)
        {
            var requested = request.Headers.AcceptLanguage
                .Select(language => language.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return requested is not null
                && requested.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : "zh-CN";
        }

        private static GeoDivisionView Localize(GeoDivisionView region, string culture) =>
            string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(region.AsciiName)
                ? region with { Name = region.AsciiName }
                : region;

        private GeoDivisionTreeView Localize(GeoDivisionTreeView node, string culture)
        {
            var name = _geoDivisions.TryGetValue(node.Id, out var region)
                ? Localize(region, culture).Name
                : node.Name;
            return node with
            {
                Name = name,
                Children = node.Children.Select(child => Localize(child, culture)).ToList()
            };
        }

        private string AirportETag(Guid id) => $"\"{id:N}-{_airports[id].Version}\"";

        private static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T body, string? eTag = null)
        {
            var response = new HttpResponseMessage(statusCode) { Content = JsonContent.Create(body) };
            if (eTag is not null)
            {
                response.Headers.ETag = new EntityTagHeaderValue(eTag);
            }

            return response;
        }

        private sealed record AirportState(AirportResponse Airport, int Version);

        private sealed class GeoDivisionCreatePayload
        {
            public int Level { get; init; }
            public string? Kind { get; init; }
            public string? CountryCode { get; init; }
            public string? SubdivisionCode { get; init; }
            public string? Code { get; init; }
            public string? Name { get; init; }
            public string? AsciiName { get; init; }
            public string? LocalName { get; init; }
            public double? Latitude { get; init; }
            public double? Longitude { get; init; }
            public long? Population { get; init; }
            public Guid? ParentId { get; init; }
        }

        private sealed class GeoDivisionUpdatePayload
        {
            public string? Name { get; init; }
            public string? AsciiName { get; init; }
            public string? LocalName { get; init; }
            public string? Code { get; init; }
            public double? Latitude { get; init; }
            public double? Longitude { get; init; }
            public long? Population { get; init; }
            public Guid? ParentId { get; init; }
            public string? Kind { get; init; }
        }
    }

    private sealed class StaticOidcConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private static readonly OpenIdConnectConfiguration Configuration = new()
        {
            Issuer = "http://localhost:5101/",
            AuthorizationEndpoint = "http://localhost:5101/connect/authorize",
            TokenEndpoint = "http://localhost:5101/connect/token"
        };

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Configuration);
        }

        public void RequestRefresh()
        {
        }
    }
}
