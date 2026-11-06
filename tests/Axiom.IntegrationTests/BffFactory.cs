// 为 BFF 集成测试替换用户认证与网关下游，保持真实的路由与 CSRF 链路。
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.Bff;
using Axiom.Bff.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Axiom.IntegrationTests;

public sealed class BffFactory : WebApplicationFactory<BffApplicationMarker>
{
    public const string KNOWN_ETAG = "\"1\"";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SCHEME;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SCHEME, _ => { });
            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new StaticOidcConfigurationManager());
            services.AddHttpClient<NewsGatewayClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new NewsStubHandler());
        });
    }

    private sealed class NewsStubHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization?.Scheme != "Bearer" ||
                request.Headers.Authorization.Parameter != "test-access-token")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/api/news/articles") is false)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/articles", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        items = new[] { Article(Guid.Parse("11111111-1111-1111-1111-111111111111"), "测试新闻") },
                        totalCount = 1,
                        page = 1,
                        pageSize = 20
                    })
                };
            }

            if (request.Method == HttpMethod.Post)
            {
                var id = Guid.NewGuid();
                var article = Article(id, "新建新闻");
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(article)
                };
                response.Headers.ETag = new EntityTagHeaderValue(KNOWN_ETAG);
                response.Headers.Location = new Uri($"/api/news/articles/{id}", UriKind.Relative);
                return response;
            }

            if (request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
            {
                if (!request.Headers.TryGetValues("If-Match", out var ifMatch))
                {
                    return new HttpResponseMessage(HttpStatusCode.PreconditionRequired);
                }

                if (!string.Equals(ifMatch.ToString(), KNOWN_ETAG, StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                }

                if (request.Method == HttpMethod.Delete)
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(Article(Guid.NewGuid(), "更新后的新闻"))
                };
                response.Headers.ETag = new EntityTagHeaderValue("\"2\"");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        private static object Article(Guid id, string title) => new
        {
            id,
            title,
            summary = "摘要",
            content = "正文",
            category = "公司动态",
            status = "Draft",
            author = "测试作者",
            publishedAt = (DateTimeOffset?)null,
            createdAt = DateTimeOffset.UnixEpoch,
            updatedAt = DateTimeOffset.UnixEpoch
        };
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
