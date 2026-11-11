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
    internal static readonly Guid RegionCountryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid RegionAdmin1Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid RegionCityId = Guid.Parse("44444444-4444-4444-4444-444444444444");

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
            services.AddHttpClient<RegionClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new GatewayStubHandler());
        });
    }

    private sealed class GatewayStubHandler : HttpMessageHandler
    {
        private const string ManageToken = "test-access-token";
        private const string ReadToken = "test-read-access-token";
        private readonly Dictionary<Guid, AirportState> _airports = new();
        private readonly Dictionary<Guid, RegionView> _regions = new();

        public GatewayStubHandler()
        {
            _airports.Add(AirportId, new AirportState(
                new AirportResponse(AirportId, "SHA", "上海浦东国际机场", "CN", "上海", 31.1443, 121.8083),
                1));
            _regions.Add(RegionCountryId, new RegionView(
                RegionCountryId, null, 1, "CN", null, "CN", "中国", "China", "中国", null, null, 1400000000, null));
            _regions.Add(RegionAdmin1Id, new RegionView(
                RegionAdmin1Id, RegionCountryId, 2, "CN", "JS", "JS", "江苏", "Jiangsu", "江苏", null, null, 85000000, null));
            _regions.Add(RegionCityId, new RegionView(
                RegionCityId, RegionAdmin1Id, 3, "CN", "JS", "SZ", "苏州", "Suzhou", "苏州", 31.3, 120.6, 12000000, null));
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

            if (path == "/api/regions")
            {
                return await RegionCollectionAsync(request, canManage, cancellationToken);
            }

            if (path == "/api/regions/tree")
            {
                return RegionTree(request);
            }

            if (path.StartsWith("/api/regions/", StringComparison.Ordinal) && path.EndsWith("/children"))
            {
                return RegionChildren(path);
            }

            if (path.StartsWith("/api/regions/", StringComparison.Ordinal))
            {
                return await RegionItemAsync(request, path, canManage, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage AirportCollection()
        {
            var items = _airports.Values.Select(state => state.Airport).OrderBy(airport => airport.Code).ToList();
            return Json(HttpStatusCode.OK, new PagedResponse<AirportResponse>(items, items.Count, 1, 20));
        }

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
                IEnumerable<RegionView> regions = _regions.Values;
                if (query.TryGetValue("level", out var levelValue) && int.TryParse(levelValue.ToString(), out var parsedLevel))
                {
                    regions = regions.Where(region => region.Level == parsedLevel);
                }

                if (query.TryGetValue("countryCode", out var countryCode) && !string.IsNullOrWhiteSpace(countryCode))
                {
                    regions = regions.Where(region => region.CountryCode == countryCode.ToString().ToUpperInvariant());
                }

                if (query.TryGetValue("admin1Code", out var admin1Code) && !string.IsNullOrWhiteSpace(admin1Code))
                {
                    regions = regions.Where(region => region.Admin1Code == admin1Code.ToString());
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
                var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Json(HttpStatusCode.OK, new PagedResponse<RegionView>(items, ordered.Count, page, pageSize));
            }

            if (request.Method != HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            var input = await request.Content!.ReadFromJsonAsync<RegionCreatePayload>(cancellationToken);
            var level = ParseLevel(input?.Level);
            if (input?.CountryCode is null || input.Name is null || level is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var normalizedCountry = input.CountryCode.ToUpperInvariant();
            var code = (input.Code ?? input.Name).ToUpperInvariant();
            if (_regions.Values.Any(region =>
                    region.CountryCode == normalizedCountry && region.Level == level && region.Code == code))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            }

            var id = Guid.NewGuid();
            var region = new RegionView(
                id,
                level == 1 ? null : input.ParentId,
                level.Value,
                normalizedCountry,
                input.Admin1Code,
                code,
                input.Name,
                input.AsciiName,
                input.LocalName,
                input.Latitude,
                input.Longitude,
                input.Population,
                null);
            _regions.Add(id, region);
            var response = Json(HttpStatusCode.Created, region);
            response.Headers.Location = new Uri($"/api/regions/{id}", UriKind.Relative);
            return response;
        }

        private HttpResponseMessage RegionTree(HttpRequestMessage request)
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var countryCode = query["countryCode"].ToString().ToUpperInvariant();
            var country = _regions.Values.FirstOrDefault(region => region.Level == 1 && region.CountryCode == countryCode);
            return country is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json(HttpStatusCode.OK, BuildTree(country));
        }

        private RegionTreeView BuildTree(RegionView region) => new(
            region.Id,
            region.Level,
            region.CountryCode,
            region.Admin1Code,
            region.Code,
            region.Name,
            region.Population,
            _regions.Values
                .Where(child => child.ParentId == region.Id)
                .OrderBy(child => child.Code)
                .Select(BuildTree)
                .ToList());

        // 单级子级：childCount 渲染展开箭头，sameNameChildCount 支撑直辖市合并展示。
        private HttpResponseMessage RegionChildren(string path)
        {
            var idSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries) switch
            {
                [_, _, var id, _] when Guid.TryParse(id, out var parsed) => parsed,
                _ => Guid.Empty
            };

            if (!_regions.TryGetValue(idSegment, out var parent))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var items = _regions.Values
                .Where(child => child.ParentId == parent.Id)
                .OrderBy(child => child.Code)
                .Select(child => new
                {
                    child.Id,
                    child.Level,
                    child.CountryCode,
                    child.Admin1Code,
                    child.Code,
                    child.Name,
                    child.Population,
                    childCount = _regions.Values.Count(grandchild => grandchild.ParentId == child.Id),
                    sameNameChildCount = _regions.Values.Count(grandchild =>
                        grandchild.ParentId == child.Id
                        && string.Equals(grandchild.Name?.Trim(), child.Name?.Trim(), StringComparison.Ordinal))
                }).ToList();
            return Json(HttpStatusCode.OK, items);
        }

        private async Task<HttpResponseMessage> RegionItemAsync(
            HttpRequestMessage request,
            string path,
            bool canManage,
            CancellationToken cancellationToken)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (!Guid.TryParse(segments[2], out var id) || !_regions.TryGetValue(id, out var region))
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
                return Json(HttpStatusCode.OK, region);
            }

            if (!canManage)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (request.Method == HttpMethod.Delete)
            {
                if (_regions.Values.Any(child => child.ParentId == id))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                _regions.Remove(id);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method != HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            }

            var input = await request.Content!.ReadFromJsonAsync<RegionUpdatePayload>(cancellationToken);
            if (input is null || input.ParentId == id)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var code = string.IsNullOrWhiteSpace(input.Code) ? region.Code : input.Code.ToUpperInvariant();
            if (_regions.Values.Any(other =>
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
                ParentId = input.ParentId ?? region.ParentId
            };
            _regions[id] = updated;
            return Json(HttpStatusCode.OK, updated);
        }

        private string AirportETag(Guid id) => $"\"{id:N}-{_airports[id].Version}\"";

        private static int? ParseLevel(string? level) => level?.Trim().ToUpperInvariant() switch
        {
            "1" or "COUNTRY" => 1,
            "2" or "ADMIN1" => 2,
            "3" or "CITY" => 3,
            _ => null
        };

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

        private sealed class RegionCreatePayload
        {
            public string? Level { get; init; }
            public string? CountryCode { get; init; }
            public string? Admin1Code { get; init; }
            public string? Code { get; init; }
            public string? Name { get; init; }
            public string? AsciiName { get; init; }
            public string? LocalName { get; init; }
            public double? Latitude { get; init; }
            public double? Longitude { get; init; }
            public long? Population { get; init; }
            public Guid? ParentId { get; init; }
        }

        private sealed class RegionUpdatePayload
        {
            public string? Name { get; init; }
            public string? AsciiName { get; init; }
            public string? LocalName { get; init; }
            public string? Code { get; init; }
            public double? Latitude { get; init; }
            public double? Longitude { get; init; }
            public long? Population { get; init; }
            public Guid? ParentId { get; init; }
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
