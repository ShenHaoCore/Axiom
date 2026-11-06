// 行政区浏览页：国家选择 + 层级树 + 搜索列表。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.Regions;

public sealed class IndexModel(RegionClient regionClient) : PageModel
{
    // 国家下拉列表
    public IReadOnlyList<RegionView> Countries { get; private set; } = [];

    // 层级树（默认选中国家）
    public RegionTreeView? Tree { get; private set; }

    // 搜索结果
    public IReadOnlyList<RegionView> SearchResults { get; private set; } = [];
    public int SearchTotalCount { get; private set; }

    // 选中的国家代码（URL 参数 / 表单）
    public string? SelectedCountryCode { get; private set; }

    public string? SearchQuery { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? countryCode,
        string? q,
        CancellationToken cancellationToken = default)
    {
        // 总是拉国家列表做下拉
        var countriesResponse = await regionClient.ListAsync(1, 250, level: 1, null, null, null, cancellationToken);
        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = countriesResponse.StatusCode == HttpStatusCode.Unauthorized
                ? null // 让 Challenge 处理
                : "无法加载国家列表。";
            return HandleStatus(countriesResponse.StatusCode);
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<RegionView>>(cancellationToken);
        Countries = countriesPage?.Items ?? [];

        // 选中国家：优先参数 → 默认列表第一个
        SelectedCountryCode = !string.IsNullOrWhiteSpace(countryCode)
            ? countryCode.Trim().ToUpperInvariant()
            : Countries.FirstOrDefault()?.CountryCode;

        // 搜索模式
        if (!string.IsNullOrWhiteSpace(q))
        {
            SearchQuery = q.Trim();
            var searchResponse = await regionClient.ListAsync(
                1, 100, null, SelectedCountryCode, null, SearchQuery, cancellationToken);

            if (!searchResponse.IsSuccessStatusCode)
            {
                ErrorMessage = searchResponse.StatusCode == HttpStatusCode.Unauthorized
                    ? null
                    : "搜索行政区失败。";
                return HandleStatus(searchResponse.StatusCode);
            }

            var page = await searchResponse.Content.ReadFromJsonAsync<PagedResponse<RegionView>>(cancellationToken);
            SearchResults = page?.Items ?? [];
            SearchTotalCount = page?.TotalCount ?? 0;
            return Page();
        }

        // 默认：拉层级树
        if (SelectedCountryCode is null)
        {
            return Page();
        }

        var treeResponse = await regionClient.GetTreeAsync(SelectedCountryCode, depth: 3, cancellationToken);
        if (!treeResponse.IsSuccessStatusCode)
        {
            ErrorMessage = treeResponse.StatusCode == HttpStatusCode.Unauthorized
                ? null
                : $"无法加载 {SelectedCountryCode} 的行政区层级。";
            return HandleStatus(treeResponse.StatusCode);
        }

        Tree = await treeResponse.Content.ReadFromJsonAsync<RegionTreeView>(cancellationToken);
        return Page();
    }

    private IActionResult HandleStatus(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => Challenge(),
            HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => Page()
        };
}
