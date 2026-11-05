// 验证 Web 的 OIDC 挑战、访问令牌转发和机场新增页面。
using System.Net;
using System.Text.RegularExpressions;
using Axiom.SharedKernel.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Axiom.IntegrationTests;

public sealed class WebEndpointTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task Get_without_user_challenges_oidc_with_pkce()
    {
        using var response = await _client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location;
        location.Should().NotBeNull();
        location!.AbsolutePath.Should().Be("/connect/authorize");
        var query = QueryHelpers.ParseQuery(location.Query);
        query["client_id"].ToString().Should().Be("axiom-web");
        query["response_type"].ToString().Should().Be("code");
        query["redirect_uri"].ToString().Should().Be("http://localhost/callback");
        query["code_challenge"].ToString().Should().NotBeNullOrEmpty();
        query["code_challenge_method"].ToString().Should().Be("S256");
        query["scope"].ToString().Should().Contain(ReferenceDataPermissions.READ)
            .And.Contain(ReferenceDataPermissions.MANAGE);
    }

    [Fact]
    public async Task Get_with_user_forwards_token_and_lists_airports()
    {
        using var request = CreateRequest(HttpMethod.Get, "/");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("SHA").And.Contain("上海浦东国际机场");
    }

    [Fact]
    public async Task Post_with_valid_form_creates_airport_through_gateway()
    {
        using var getRequest = CreateRequest(HttpMethod.Get, "/");
        using var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await getResponse.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        tokenMatch.Success.Should().BeTrue();

        using var postRequest = CreateRequest(HttpMethod.Post, "/");
        postRequest.Content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", WebUtility.HtmlDecode(tokenMatch.Groups[1].Value)),
            new("Input.Code", "PVG"),
            new("Input.Name", "上海浦东国际机场"),
            new("Input.CountryCode", "CN"),
            new("Input.City", "上海"),
            new("Input.Latitude", "31.1443"),
            new("Input.Longitude", "121.8083")
        ]);

        using var response = await _client.SendAsync(postRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be("/");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthHandler.PERMISSIONS_HEADER, ReferenceDataPermissions.READ);
        return request;
    }
}
