// 验证 AuthHub 从 OIDC 的 ui_locales 参数协商登录页语言。
using Axiom.AuthHub.Localization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Axiom.IntegrationTests;

public sealed class UiLocalesRequestCultureProviderTests
{
    private readonly UiLocalesRequestCultureProvider _provider = new();

    [Fact]
    public async Task Authorize_request_ui_locales_selects_culture()
    {
        var context = CreateContext("/connect/authorize?client_id=axiom-web&ui_locales=en");

        var result = await _provider.DetermineProviderCultureResult(context);

        result!.Cultures.Select(culture => culture.ToString()).Should().Equal("en");
        result.UICultures.Select(culture => culture.ToString()).Should().Equal("en");
    }

    [Fact]
    public async Task Login_redirect_reads_ui_locales_from_return_url()
    {
        var returnUrl = System.Net.WebUtility.UrlEncode("/connect/authorize?client_id=axiom-web&ui_locales=en");
        var context = CreateContext($"/Account/Login?ReturnUrl={returnUrl}");

        var result = await _provider.DetermineProviderCultureResult(context);

        result!.Cultures.Select(culture => culture.ToString()).Should().Equal("en");
    }

    [Fact]
    public async Task Space_separated_ui_locales_uses_first_normalized_value()
    {
        var context = CreateContext("/connect/authorize?ui_locales=EN-us%20zh-CN");

        var result = await _provider.DetermineProviderCultureResult(context);

        result!.Cultures.Select(culture => culture.ToString()).Should().Equal("en-US");
    }

    [Fact]
    public async Task Missing_or_invalid_ui_locales_falls_through_to_next_provider()
    {
        (await _provider.DetermineProviderCultureResult(CreateContext("/connect/authorize")))
            .Should().BeNull();
        (await _provider.DetermineProviderCultureResult(CreateContext("/connect/authorize?ui_locales=--")))
            .Should().BeNull();
    }

    private static DefaultHttpContext CreateContext(string url)
    {
        var context = new DefaultHttpContext();
        var index = url.IndexOf('?');
        context.Request.Path = index < 0 ? url : url[..index];
        context.Request.QueryString = index < 0 ? QueryString.Empty : new QueryString(url[index..]);
        return context;
    }
}
