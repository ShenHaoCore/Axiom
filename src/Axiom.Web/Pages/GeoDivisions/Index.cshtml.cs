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

namespace Axiom.Web.Pages.GeoDivisions;

public sealed class IndexModel(
    GeoDivisionClient regionClient,
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    // 国家列表：层级树的根节点 + 搜索下拉筛选项
    public IReadOnlyList<GeoDivisionView> Countries { get; private set; } = [];

    // Kind 显示名（来自枚举字典 reference_enums，显示名由字典表维护）
    public IReadOnlyDictionary<string, string> KindNames { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Kind 显示名；字典缺失时回退 code，再回退深度标签（非省市区文案）。</summary>
    public string KindNameFor(string? kind, int level)
    {
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var code = kind.Trim().ToLowerInvariant();
            if (KindNames.TryGetValue(code, out var name))
            {
                return name;
            }

            return code;
        }

        return level is >= 1 and <= 4 ? localizer[$"GeoDivisions.Level.{level}"].Value : $"L{level}";
    }

    // 搜索结果
    public IReadOnlyList<GeoDivisionView> SearchResults { get; private set; } = [];
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
        await LoadKindNamesAsync(cancellationToken);
        // 总是拉国家列表：树的根节点 + 下拉筛选
        var countriesResponse = await regionClient.ListAsync(1, 250, level: 1, null, null, null, cancellationToken);
        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = countriesResponse.StatusCode == HttpStatusCode.Unauthorized
                ? null
                : localizer["GeoDivisions.LoadCountriesFailed"].Value;
            return HandleStatus(countriesResponse.StatusCode);
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
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
                    : localizer["GeoDivisions.SearchFailed"].Value;
                return HandleStatus(searchResponse.StatusCode);
            }

            var page = await searchResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
            SearchResults = page?.Items ?? [];
            SearchTotalCount = page?.TotalCount ?? 0;
        }

        return Page();
    }

    // 惰性加载：返回某节点子级的局部视图（含直辖市合并与展开箭头判断）。
    public async Task<IActionResult> OnGetChildrenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await LoadKindNamesAsync(cancellationToken);
        var response = await regionClient.GetChildrenAsync(id, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? Challenge()
                : StatusCode(StatusCodes.Status502BadGateway);
        }

        var children = await response.Content.ReadFromJsonAsync<List<GeoDivisionChildView>>(cancellationToken) ?? [];
        return Partial("_GeoDivisionChildren", children.Select(child =>
            GeoDivisionNodeViewModel.FromChild(child, KindNameFor(child.Kind, child.Level))).ToList());
    }

    private async Task LoadKindNamesAsync(CancellationToken cancellationToken)
    {
        // 不带 category 过滤：取全部枚举后按 geo_division.kind* 分域合并（含各国专属类型）。
        using var response = await enumClient.ListAsync(
            category: null,
            pageSize: 200,
            cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        if (page is null)
        {
            return;
        }

        KindNames = page.Items
            .Where(IsGeoDivisionKindCategory)
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
    }

    private static bool IsGeoDivisionKindCategory(ReferenceEnumView item) =>
        item.Category.Equals(ReferenceEnumClient.GeoDivisionKindCategory, StringComparison.Ordinal)
        || item.Category.StartsWith(ReferenceEnumClient.GeoDivisionKindCategoryPrefix, StringComparison.Ordinal);

    private IActionResult HandleStatus(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => Challenge(),
            HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => Page()
        };
}
