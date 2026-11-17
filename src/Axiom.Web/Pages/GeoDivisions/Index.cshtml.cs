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
    // 国家列表：层级树的根节点。
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

    public string? SearchQuery { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? q,
        CancellationToken cancellationToken = default)
    {
        KindNames = await enumClient.LoadGeoDivisionKindNamesAsync(cancellationToken);
        // 总是拉国家列表：树的根节点。
        var countriesResponse = await regionClient.ListAsync(1, 250, level: 1, null, null, null, cancellationToken);
        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = countriesResponse.StatusCode == HttpStatusCode.Unauthorized
                ? null
                : localizer["GeoDivisions.LoadCountriesFailed"].Value;
            return this.HandleAuthStatus(countriesResponse.StatusCode) ?? Page();
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
        Countries = countriesPage?.Items ?? [];

        // 搜索模式：树不受搜索影响，始终展示全部国家。
        if (!string.IsNullOrWhiteSpace(q))
        {
            SearchQuery = q.Trim();
            var searchResponse = await regionClient.ListAsync(
                1, 100, null, null, null, SearchQuery, cancellationToken);

            if (!searchResponse.IsSuccessStatusCode)
            {
                ErrorMessage = searchResponse.StatusCode == HttpStatusCode.Unauthorized
                    ? null
                    : localizer["GeoDivisions.SearchFailed"].Value;
                return this.HandleAuthStatus(searchResponse.StatusCode) ?? Page();
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
        KindNames = await enumClient.LoadGeoDivisionKindNamesAsync(cancellationToken);
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

}
