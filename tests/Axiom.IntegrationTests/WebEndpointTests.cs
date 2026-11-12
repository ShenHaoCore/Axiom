// 验证 Web 的 OIDC 挑战、访问令牌转发及机场和行政区 CRUD 页面。
using System.Net;
using System.Text.RegularExpressions;
using Axiom.SharedKernel.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Axiom.IntegrationTests;

public sealed class WebEndpointTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private const string Manage = ReferenceDataPermissions.MANAGE;
    private const string Read = ReferenceDataPermissions.READ;
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
        query["scope"].ToString().Should().Contain(Read).And.Contain(Manage);
    }

    [Fact]
    public async Task Get_with_user_forwards_token_and_lists_airports()
    {
        using var request = CreateRequest(HttpMethod.Get, "/");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("SHA").And.Contain("上海浦东国际机场");
        content.Should().Contain("href=\"/Airports/Create\"").And.NotContain("id=\"airport-create-form\"");
        content.Should().Contain($"/Airports/Edit/{WebFactory.AirportId}");
    }

    [Fact]
    public async Task Post_with_valid_form_creates_airport_through_gateway()
    {
        using var response = await PostFormAsync(
            "/Airports/Create",
            AirportFields("PVG", "上海浦东国际机场"),
            Manage);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task Get_airport_edit_loads_detail_and_etag()
    {
        var form = await GetFormAsync($"/Airports/Edit/{WebFactory.AirportId}");

        WebUtility.HtmlDecode(form.Html).Should().Contain("SHA").And.Contain("上海浦东国际机场");
        form.Html.Should().Contain("id=\"airport-delete-form\"");
        ExtractInput(form.Html, "ETag").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Airport_update_and_delete_roundtrip()
    {
        var id = await CreateAirportAsync("PEK", "北京首都国际机场");
        var uri = $"/Airports/Edit/{id}";
        var form = await GetFormAsync(uri);
        var eTag = ExtractInput(form.Html, "ETag");

        using var updateResponse = await PostFormAsync(
            uri,
            AirportFields("PEK", "北京首都国际机场（更新）").Append(new("ETag", eTag)),
            Manage);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var updatedForm = await GetFormAsync(uri);
        WebUtility.HtmlDecode(updatedForm.Html).Should().Contain("北京首都国际机场（更新）");
        var updatedETag = ExtractInput(updatedForm.Html, "ETag");
        updatedETag.Should().NotBe(eTag);

        using var deleteResponse = await PostFormAsync(
            $"{uri}?handler=Delete",
            [new("ETag", updatedETag)],
            Manage,
            uri);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        deleteResponse.Headers.Location?.OriginalString.Should().Be("/");

        using var getRequest = CreateRequest(HttpMethod.Get, uri);
        using var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Airport_update_with_stale_etag_shows_conflict()
    {
        var id = await CreateAirportAsync("CAN", "广州白云国际机场");
        var uri = $"/Airports/Edit/{id}";
        var staleETag = ExtractInput((await GetFormAsync(uri)).Html, "ETag");
        using var firstUpdate = await PostFormAsync(
            uri,
            AirportFields("CAN", "广州白云国际机场（第一次更新）").Append(new("ETag", staleETag)),
            Manage);
        firstUpdate.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var conflictResponse = await PostFormAsync(
            uri,
            AirportFields("CAN", "广州白云国际机场（冲突更新）").Append(new("ETag", staleETag)),
            Manage);

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await conflictResponse.Content.ReadAsStringAsync());
        content.Should().Contain("内容已被其他请求修改");
    }

    [Fact]
    public async Task Airport_update_with_missing_etag_shows_local_validation()
    {
        var uri = $"/Airports/Edit/{WebFactory.AirportId}";

        using var response = await PostFormAsync(
            uri,
            AirportFields("SHA", "上海浦东国际机场"),
            Manage,
            uri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("缺少并发令牌");
    }

    [Fact]
    public async Task Airport_write_with_only_read_permission_returns_403()
    {
        var uri = $"/Airports/Edit/{WebFactory.AirportId}";
        var form = await GetFormAsync(uri);
        var eTag = ExtractInput(form.Html, "ETag");

        using var response = await PostFormAsync(
            uri,
            AirportFields("SHA", "上海浦东国际机场").Append(new("ETag", eTag)),
            Read,
            uri);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_unknown_airport_returns_404()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/Airports/Edit/{Guid.NewGuid()}");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Region_pages_expose_create_and_edit_entries()
    {
        using var indexRequest = CreateRequest(HttpMethod.Get, "/Regions?countryCode=CN");
        using var indexResponse = await _client.SendAsync(indexRequest);
        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var index = await indexResponse.Content.ReadAsStringAsync();
        // 层级树服务端只渲染国家根节点，子级在展开时惰性加载。
        index.Should().Contain("/Regions/Create").And.Contain(
            $"/Regions/Detail/{WebFactory.RegionCountryCode}/{WebFactory.RegionCountryCode}");

        using var childrenRequest = CreateRequest(
            HttpMethod.Get,
            $"/Regions?handler=Children&country={WebFactory.RegionCountryCode}&code={WebFactory.RegionCountryCode}");
        using var childrenResponse = await _client.SendAsync(childrenRequest);
        childrenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var children = WebUtility.HtmlDecode(await childrenResponse.Content.ReadAsStringAsync());
        children.Should().Contain("江苏").And.Contain(
            $"/Regions/Edit/{WebFactory.RegionCountryCode}/{WebFactory.RegionProvinceCode}");

        using var grandChildrenRequest = CreateRequest(
            HttpMethod.Get,
            $"/Regions?handler=Children&country={WebFactory.RegionCountryCode}&code={WebFactory.RegionProvinceCode}");
        using var grandChildrenResponse = await _client.SendAsync(grandChildrenRequest);
        grandChildrenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var grandChildren = WebUtility.HtmlDecode(await grandChildrenResponse.Content.ReadAsStringAsync());
        grandChildren.Should().Contain("苏州").And.Contain(
            $"/Regions/Edit/{WebFactory.RegionCountryCode}/{WebFactory.RegionCityCode}");

        var edit = await GetFormAsync(
            $"/Regions/Edit/{WebFactory.RegionCountryCode}/{WebFactory.RegionCityCode}");
        WebUtility.HtmlDecode(edit.Html).Should().Contain("苏州");
        Regex.IsMatch(
            edit.Html,
            $"<option(?=[^>]*value=\"{WebFactory.RegionProvinceCode}\")(?=[^>]*selected)[^>]*>",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)).Should().BeTrue("当前父级应在下拉框中选中");
    }

    [Fact]
    public async Task Region_create_update_delete_roundtrip()
    {
        var (countryCode, _) = await CreateRegionAsync(0, "XZ", null, "XZ", "测试国家");
        await CreateRegionAsync(1, "XZ", countryCode, "XA", "测试省");
        var (_, cityCode) = await CreateRegionAsync(2, "XZ", "XA", "XC", "测试市");
        var editUri = $"/Regions/Edit/{countryCode}/{cityCode}";

        using var updateResponse = await PostFormAsync(
            editUri,
            RegionFields("XZ", "XA", "XC", "测试市（更新）"),
            Manage,
            editUri);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        updateResponse.Headers.Location?.OriginalString.Should().Be($"/Regions/Detail/{countryCode}/{cityCode}");

        using var detailRequest = CreateRequest(HttpMethod.Get, $"/Regions/Detail/{countryCode}/{cityCode}");
        using var detailResponse = await _client.SendAsync(detailRequest);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await detailResponse.Content.ReadAsStringAsync()).Should().Contain("测试市（更新）");

        await DeleteRegionAsync(countryCode, cityCode);
        await DeleteRegionAsync(countryCode, "XA");
        await DeleteRegionAsync(countryCode, countryCode);

        using var missingRequest = CreateRequest(HttpMethod.Get, $"/Regions/Detail/{countryCode}/NOPE");
        using var missingResponse = await _client.SendAsync(missingRequest);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Region_duplicate_code_shows_conflict()
    {
        using var response = await PostFormAsync(
            "/Regions/Create?depth=0&countryCode=CN",
            RegionFields("CN", null, "CN", "重复国家"),
            Manage,
            "/Regions/Create?depth=0&countryCode=CN");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("行政区代码已存在");
    }

    [Fact]
    public async Task Region_delete_with_children_shows_validation_error()
    {
        var uri = $"/Regions/Detail/{WebFactory.RegionCountryCode}/{WebFactory.RegionProvinceCode}";

        using var response = await PostFormAsync(
            $"{uri}?handler=Delete",
            [new("CountryCode", "CN")],
            Manage,
            uri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("仍有子节点");
        // 删除被拒后子级仍在：通过惰性加载接口确认江苏下还有苏州。
        using var treeRequest = CreateRequest(
            HttpMethod.Get,
            $"/Regions?handler=Children&country={WebFactory.RegionCountryCode}&code={WebFactory.RegionProvinceCode}");
        using var treeResponse = await _client.SendAsync(treeRequest);
        treeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await treeResponse.Content.ReadAsStringAsync()).Should().Contain("苏州");
    }

    [Fact]
    public async Task Region_write_with_only_read_permission_returns_403()
    {
        using var response = await PostFormAsync(
            "/Regions/Create?depth=1&countryCode=ZY",
            RegionFields("ZY", "ZY", "ZY1", "只读测试国家"),
            Read,
            "/Regions/Create?depth=1&countryCode=ZY");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_unknown_region_returns_404()
    {
        using var request = CreateRequest(HttpMethod.Get, "/Regions/Edit/ZZ/NOPE");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_with_english_accept_language_renders_english_ui()
    {
        using var request = CreateRequest(HttpMethod.Get, "/");
        request.Headers.Add("Accept-Language", "en");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("Airport list").And.Contain("Create airport");
        content.Should().NotContain("机场列表");
    }

    [Fact]
    public async Task Post_invalid_form_with_english_accept_language_shows_localized_validation()
    {
        using var getRequest = CreateRequest(HttpMethod.Get, "/Airports/Create");
        getRequest.Headers.Add("Accept-Language", "en");
        using var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = ExtractInput(html, "__RequestVerificationToken");

        using var postRequest = CreateRequest(HttpMethod.Post, "/Airports/Create");
        postRequest.Headers.Add("Accept-Language", "en");
        postRequest.Content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("Input.Name", "Shanghai Pudong International Airport"),
            new("Input.CountryCode", "CN")
        ]);

        using var response = await _client.SendAsync(postRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        content.Should().Contain("The airport code is required.");
    }

    [Fact]
    public async Task Challenge_forwards_selected_culture_as_ui_locales()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Accept-Language", "en");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        query["ui_locales"].ToString().Should().Be("en");
    }

    [Fact]
    public async Task Culture_endpoint_sets_culture_cookie()
    {
        using var response = await _client.GetAsync("/culture?culture=en&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be("/");
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.Should().Contain(".AspNetCore.Culture=c%3Den%7Cuic%3Den");
    }

    [Fact]
    public async Task Culture_endpoint_rejects_unsupported_culture()
    {
        using var response = await _client.GetAsync("/culture?culture=fr&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateAirportAsync(string code, string name)
    {
        using var response = await PostFormAsync("/Airports/Create", AirportFields(code, name), Manage);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var listRequest = CreateRequest(HttpMethod.Get, "/");
        using var listResponse = await _client.SendAsync(listRequest);
        var html = await listResponse.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            $"<td>{code}</td>[\\s\\S]*?href=\"/Airports/Edit/([0-9a-fA-F-]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        match.Success.Should().BeTrue($"机场 {code} 应出现在列表中");
        return Guid.Parse(match.Groups[1].Value);
    }

    private async Task<(string CountryCode, string Code)> CreateRegionAsync(
        int depth,
        string countryCode,
        string? parentCode,
        string code,
        string name)
    {
        var uri = $"/Regions/Create?depth={depth}&countryCode={countryCode}";
        using var response = await PostFormAsync(
            uri,
            RegionFields(countryCode, parentCode, code, name),
            Manage,
            uri);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.OriginalString;
        location.Should().Contain("/Regions/Detail/");
        var segments = location!.Split('?')[0].TrimEnd('/').Split('/');
        return (segments[^2], segments[^1]);
    }

    private async Task DeleteRegionAsync(string countryCode, string code)
    {
        var uri = $"/Regions/Detail/{countryCode}/{code}";
        using var response = await PostFormAsync(
            $"{uri}?handler=Delete",
            [new("CountryCode", countryCode)],
            Manage,
            uri);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Contain("countryCode=");
    }

    private async Task<(string Token, string Html)> GetFormAsync(string uri, string permissions = Read)
    {
        using var request = CreateRequest(HttpMethod.Get, uri, permissions);
        using var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return (ExtractInput(html, "__RequestVerificationToken"), html);
    }

    private async Task<HttpResponseMessage> PostFormAsync(
        string uri,
        IEnumerable<KeyValuePair<string, string>> fields,
        string permissions = Read,
        string? getUri = null)
    {
        var form = await GetFormAsync(getUri ?? uri, permissions);
        using var request = CreateRequest(HttpMethod.Post, uri, permissions);
        request.Content = new FormUrlEncodedContent(
            fields.Append(new("__RequestVerificationToken", form.Token)));
        return await _client.SendAsync(request);
    }

    private static KeyValuePair<string, string>[] AirportFields(string code, string name) =>
    [
        new("Input.Code", code),
        new("Input.Name", name),
        new("Input.CountryCode", "CN"),
        new("Input.City", "上海"),
        new("Input.Latitude", "31.1443"),
        new("Input.Longitude", "121.8083")
    ];

    private static KeyValuePair<string, string>[] RegionFields(
        string countryCode,
        string? parentCode,
        string code,
        string name)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Input.CountryCode", countryCode),
            new("Input.Code", code),
            new("Input.Name", name),
            new("Input.AsciiName", name),
            new("Input.LocalName", name),
            new("Input.Latitude", "31.3"),
            new("Input.Longitude", "120.6"),
            new("Input.Population", "1000")
        };
        if (parentCode is not null)
        {
            fields.Add(new("Input.ParentCode", parentCode));
        }

        return fields.ToArray();
    }

    private static string ExtractInput(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        match.Success.Should().BeTrue($"页面应包含 {name} 输入字段");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri,
        string permissions = Read)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthHandler.PERMISSIONS_HEADER, permissions);
        return request;
    }
}
