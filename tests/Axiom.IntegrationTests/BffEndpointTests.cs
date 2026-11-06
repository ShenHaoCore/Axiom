// 验证 BFF 的 JSON 未登录语义、CSRF 防护与新闻 CRUD 透传行为。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.SharedKernel.Security;
using FluentAssertions;

namespace Axiom.IntegrationTests;

public sealed class BffEndpointTests(BffFactory factory) : IClassFixture<BffFactory>
{
    [Fact]
    public async Task Me_ReturnsJsonUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/bff/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ListNews_ReturnsArticles_WithForwardedAccessToken()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/bff/news?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("title").GetString().Should().Be("测试新闻");
    }

    [Fact]
    public async Task CreateNews_ReturnsCreatedWithEtagAndBffLocation()
    {
        var client = CreateAuthenticatedClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/bff/news")
        {
            Content = JsonContent.Create(CreateInput("新建新闻"))
        };
        request.Headers.Add("X-CSRF-Token", csrfToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.Tag.Should().Be(BffFactory.KNOWN_ETAG);
        response.Headers.Location!.ToString().Should().StartWith("/bff/news/");
    }

    [Fact]
    public async Task UpdateNews_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        var client = CreateAuthenticatedClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/bff/news/11111111-1111-1111-1111-111111111111")
        {
            Content = JsonContent.Create(CreateInput("更新新闻"))
        };
        request.Headers.Add("X-CSRF-Token", csrfToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task UpdateNews_WithStaleEtag_ReturnsConflict()
    {
        var client = CreateAuthenticatedClient();
        var csrfToken = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            "/bff/news/11111111-1111-1111-1111-111111111111")
        {
            Content = JsonContent.Create(CreateInput("更新新闻"))
        };
        request.Headers.Add("X-CSRF-Token", csrfToken);
        request.Headers.TryAddWithoutValidation("If-Match", "\"0\"");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateNews_WithoutCsrfToken_ReturnsBadRequest()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/bff/news", CreateInput("新建新闻"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("csrf_failed");
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.PERMISSIONS_HEADER,
            $"{NewsPermissions.READ},{NewsPermissions.MANAGE}");
        return client;
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/bff/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("csrfToken").GetString()!;
    }

    private static object CreateInput(string title) => new
    {
        title,
        summary = "摘要",
        content = "正文",
        category = "公司动态",
        author = "测试作者",
        status = "Draft"
    };
}
