// 为 Web 集成测试替换用户认证和网关传输边界。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axiom.IntegrationTests;

public sealed class WebFactory : WebApplicationFactory<WebApplicationMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:BackchannelAuthority"] = "http://localhost:5101"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SCHEME;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SCHEME, _ => { });
            services.AddHttpClient<ReferenceDataClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new GatewayStubHandler());
        });
    }

    private sealed class GatewayStubHandler : HttpMessageHandler
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

            if (request.RequestUri?.AbsolutePath != "/api/reference-data/airports")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new PagedResponse<AirportResponse>(
                        new[]
                        {
                            new AirportResponse(Guid.NewGuid(), "SHA", "上海浦东国际机场", "CN", "上海", 31.1443, 121.8083)
                        },
                        1,
                        1,
                        20))
                };
            }

            if (request.Method == HttpMethod.Post && request.Content is not null)
            {
                var input = await request.Content.ReadFromJsonAsync<AirportInput>(cancellationToken);
                var statusCode = input?.Code == "PVG" && input.Name == "上海浦东国际机场"
                    ? HttpStatusCode.Created
                    : HttpStatusCode.BadRequest;
                return new HttpResponseMessage(statusCode);
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }
}
