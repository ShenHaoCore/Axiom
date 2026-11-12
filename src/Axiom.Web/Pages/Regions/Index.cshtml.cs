// 行政区浏览页：单棵全量层级树（国家为根、子级按展开惰性加载）+ 搜索列表。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Regions;

public sealed class IndexModel(
    RegionClient regionClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    // 国家列表：层级树的根节点 + 搜索下拉筛选项
    public IReadOnlyList<RegionView> Countries { get; private set; } = [];

    // 搜索结果
    public IReadOnlyList<RegionView> SearchResults { get; private set; } = [];
    public int SearchTotalCount { get; private set; }

    // 搜索过滤的国家代码（URL 参数 / 表单）；「全部国家」时为 null
    public string? SelectedCountryCode { get; private set; }

    public string? SearchQuery { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? countryCode,
        string? q,
        CancellationToken cancellationToken = default)
    {
        // 总是拉国家列表：树的根节点 + 下拉筛选
        var countriesResponse = await regionClient.ListAsync(1, 250, depth: 0, null, null, null, cancellationToken);
        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = countriesResponse.StatusCode == HttpStatusCode.Unauthorized
                ? null // 让 Challenge 处理
                : localizer["Regions.LoadCountriesFailed"].Value;
            return HandleStatus(countriesResponse.StatusCode);
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<RegionView>>(cancellationToken);
        Countries = countriesPage?.Items ?? [];

        // 搜索过滤范围：countryCode=all 或未指定时跨全部国家搜索。
        var requestedCountry = countryCode?.Trim();
        SelectedCountryCode = string.IsNullOrEmpty(requestedCountry)
            || string.Equals(requestedCountry, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : requestedCountry.ToUpperInvariant();

        // 搜索模式：树不受筛选影响，始终展示全部国家。
        if (!string.IsNullOrWhiteSpace(q))
        {
            SearchQuery = q.Trim();
            var searchResponse = await regionClient.ListAsync(
                1, 100, null, SelectedCountryCode, null, SearchQuery, cancellationToken);

            if (!searchResponse.IsSuccessStatusCode)
            {
                ErrorMessage = searchResponse.StatusCode == HttpStatusCode.Unauthorized
                    ? null
                    : localizer["Regions.SearchFailed"].Value;
                return HandleStatus(searchResponse.StatusCode);
            }

            var page = await searchResponse.Content.ReadFromJsonAsync<PagedResponse<RegionView>>(cancellationToken);
            SearchResults = page?.Items ?? [];
            SearchTotalCount = page?.TotalCount ?? 0;
        }

        return Page();
    }

    // 惰性加载：返回某节点子级的局部视图（含同名合并与展开箭头判断）。
    public async Task<IActionResult> OnGetChildrenAsync(string country, string code, CancellationToken cancellationToken = default)
    {
        var response = await regionClient.GetChildrenAsync(country, code, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // 401 交给 Challenge 走登录跳转；其余错误由前端提示重试。
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? Challenge()
                : StatusCode(StatusCodes.Status502BadGateway);
        }

        var children = await response.Content.ReadFromJsonAsync<List<RegionChildView>>(cancellationToken) ?? [];
        return Partial("_RegionChildren", children.Select(RegionNodeViewModel.FromChild).ToList());
    }

    private IActionResult HandleStatus(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => Challenge(),
            HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => Page()
        };
}
