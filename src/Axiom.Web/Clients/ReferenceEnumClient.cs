// 使用当前 SSO 会话的访问令牌调用枚举字典网关。
using System.Net;
using System.Net.Http.Json;
using Axiom.ReferenceData.Domain.Constants;
using Axiom.SharedKernel.Models;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class ReferenceEnumClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string ENUMS_PATH = "/api/reference-data/enums";

    protected override string EntityPath => ENUMS_PATH;

    // 行政区 Kind 枚举字典的 Category 标识，由 Domain 层统一定义，消除 Web/Mock/API 三层硬编码。
    public static string GeoDivisionKindCategory => ReferenceEnumCategories.GeoDivisionKind;
    public static string GeoDivisionKindCategoryPrefix => $"{ReferenceEnumCategories.GeoDivisionKind}.";

    /// <summary>Kind 字典按国家分域：geo_division.kind.{country}；国家代码无效时回退通用域。</summary>
    public static string GeoDivisionKindCategoryFor(string? countryCode) =>
        GeoDivisionKinds.CategoryFor(countryCode);

    public Task<HttpResponseMessage> ListAsync(
        string? category,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"{GatewayFields.PAGE}={page}", $"{GatewayFields.PAGE_SIZE}={pageSize}" };
        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add($"{GatewayFields.CATEGORY}={Uri.EscapeDataString(category)}");
        }

        return SendAsync(HttpMethod.Get, $"{ENUMS_PATH}/?{string.Join('&', query)}", null, cancellationToken);
    }

    /// <summary>
    /// 读取行政区 Kind 字典：优先按国家分域，该国未维护时回退通用域。
    /// 返回空列表且状态码非 2xx 表示加载失败。
    /// </summary>
    public async Task<(IReadOnlyList<ReferenceEnumView> Items, HttpStatusCode StatusCode)> ListGeoDivisionKindsAsync(
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var category = GeoDivisionKindCategoryFor(countryCode);
        var (items, status) = await ReadKindItemsAsync(category, cancellationToken);
        if (items is null)
        {
            return ([], status);
        }

        if (items.Count == 0 && !string.Equals(category, GeoDivisionKindCategory, StringComparison.Ordinal))
        {
            var (fallbackItems, fallbackStatus) = await ReadKindItemsAsync(GeoDivisionKindCategory, cancellationToken);
            if (fallbackItems is null)
            {
                return ([], fallbackStatus);
            }

            items = fallbackItems;
        }

        return (items, HttpStatusCode.OK);
    }

    /// <summary>Kind 显示名字典：取全部枚举后按 geo_division.kind* 分域合并（含各国专属类型）；失败时返回空字典。</summary>
    public async Task<Dictionary<string, string>> LoadGeoDivisionKindNamesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await ListAsync(category: null, pageSize: 200, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        if (page is null)
        {
            return [];
        }

        return page.Items
            .Where(IsGeoDivisionKindCategory)
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
    }

    private static bool IsGeoDivisionKindCategory(ReferenceEnumView item) =>
        item.Category.Equals(GeoDivisionKindCategory, StringComparison.Ordinal)
        || item.Category.StartsWith(GeoDivisionKindCategoryPrefix, StringComparison.Ordinal);

    public Task<HttpResponseMessage> CreateAsync(ReferenceEnumInput input, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            ENUMS_PATH,
            JsonContent.Create(new
            {
                category = input.Category,
                code = input.Code,
                name = input.Name,
                sortOrder = input.SortOrder
            }),
            cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string eTag,
        ReferenceEnumInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            $"{ENUMS_PATH}/{id}",
            JsonContent.Create(new
            {
                name = input.Name,
                sortOrder = input.SortOrder
            }),
            cancellationToken,
            eTag);

    public Task<HttpResponseMessage> DeleteAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{ENUMS_PATH}/{id}", null, cancellationToken, eTag);

    private async Task<(List<ReferenceEnumView>? Items, HttpStatusCode StatusCode)> ReadKindItemsAsync(
        string category,
        CancellationToken cancellationToken)
    {
        using var response = await ListAsync(category, pageSize: 200, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, response.StatusCode);
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        return ((page?.Items ?? [])
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code)
            .ToList(), response.StatusCode);
    }
}
