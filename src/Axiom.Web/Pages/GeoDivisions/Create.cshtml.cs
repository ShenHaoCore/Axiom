// 新增行政区：层级由父级推导（无父级为 1）；父级树按需惰性加载。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.GeoDivisions;

public sealed class CreateModel(
    GeoDivisionClient regionClient,
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    /// <summary>与树「新增子级」一致：父级最高到 L3，子级最高 L4。</summary>
    internal const int MaxParentLevel = 3;

    [BindProperty]
    public GeoDivisionInput Input { get; set; } = new();

    public IReadOnlyList<GeoDivisionView> Countries { get; private set; } = [];

    public string SelectedParentLabel { get; private set; } = string.Empty;

    public IReadOnlyList<SelectListItem> KindOptions { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? countryCode,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var requestedCountry = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();
        Input.CountryCode = requestedCountry ?? Input.CountryCode;
        Input.ParentId = parentId;

        var deriveFailure = await DeriveLevelFromParentAsync(cancellationToken);
        if (deriveFailure is not null)
        {
            return deriveFailure;
        }

        // 国家下拉与父级同属 GET 刷新：换国家后仍带着旧父级时，丢弃父级并按新国家建 L1。
        if (requestedCountry is not null
            && Input.ParentId.HasValue
            && !string.Equals(Input.CountryCode, requestedCountry, StringComparison.Ordinal))
        {
            Input.ParentId = null;
            Input.Level = 1;
            Input.CountryCode = requestedCountry;
            SelectedParentLabel = string.Empty;
        }

        var failure = await LoadOptionsAsync(cancellationToken);
        return failure ?? Page();
    }

    /// <summary>
    /// 父级树惰性节点：
    /// q 有值 → 按国+关键字搜索可选父级；
    /// 无 parentId → 该国 L1 根；有 parentId → 其子级。
    /// </summary>
    public async Task<IActionResult> OnGetParentNodesAsync(
        string? countryCode,
        Guid? parentId,
        Guid? selectedId,
        Guid? excludeId,
        string? q = null,
        int maxParentLevel = MaxParentLevel,
        int? selectableLevel = null,
        CancellationToken cancellationToken = default)
    {
        maxParentLevel = Math.Clamp(maxParentLevel, 1, MaxParentLevel);
        var keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        // 搜索：跨已加载树，直接按名称/代码查可选父级。
        if (keyword is not null && parentId is null)
        {
            var cc = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();
            if (cc is null || cc.Length != 2)
            {
                return new JsonResult(Array.Empty<ParentTreeSelectItemDto>());
            }

            using var searchResponse = await regionClient.ListAsync(
                1, 50, level: null, cc, null, keyword, cancellationToken);
            var searchStatus = HandleStatus(searchResponse.StatusCode);
            if (searchStatus is not null)
            {
                return searchStatus;
            }

            if (!searchResponse.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            var searchPage = await searchResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
            var hits = (searchPage?.Items ?? [])
                .Where(item => excludeId is null || item.Id != excludeId.Value)
                .Where(item => item.Level <= maxParentLevel)
                .Where(item => selectableLevel is null || item.Level == selectableLevel.Value)
                .Select(item => ToDto(
                    item.Id,
                    item.Name,
                    item.Code,
                    item.Level,
                    selectedId,
                    maxParentLevel,
                    selectableLevel,
                    hasChildren: false))
                .ToList();

            return new JsonResult(hits);
        }

        if (parentId is Guid id)
        {
            using var childrenResponse = await regionClient.GetChildrenAsync(id, cancellationToken);
            var childrenStatus = HandleStatus(childrenResponse.StatusCode);
            if (childrenStatus is not null)
            {
                return childrenStatus;
            }

            if (!childrenResponse.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            var children = await childrenResponse.Content.ReadFromJsonAsync<List<GeoDivisionChildView>>(cancellationToken)
                ?? [];
            return new JsonResult(MapChildren(children, selectedId, excludeId, maxParentLevel, selectableLevel));
        }

        var country = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();
        if (country is null || country.Length != 2)
        {
            return new JsonResult(Array.Empty<ParentTreeSelectItemDto>());
        }

        using var rootsResponse = await regionClient.ListAsync(
            1, 50, level: 1, country, null, null, cancellationToken);
        var rootsStatus = HandleStatus(rootsResponse.StatusCode);
        if (rootsStatus is not null)
        {
            return rootsStatus;
        }

        if (!rootsResponse.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        var page = await rootsResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
        var roots = (page?.Items ?? [])
            .Where(item => excludeId is null || item.Id != excludeId.Value)
            .Select(item => ToDto(
                item.Id,
                item.Name,
                item.Code,
                item.Level,
                selectedId,
                maxParentLevel,
                selectableLevel,
                // 根节点默认可展开；真实子级在展开时再拉。
                hasChildren: item.Level < (selectableLevel ?? maxParentLevel)))
            .ToList();

        return new JsonResult(roots);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        Input.CountryCode = Input.CountryCode?.Trim().ToUpperInvariant();

        var deriveFailure = await DeriveLevelFromParentAsync(cancellationToken);
        if (deriveFailure is not null)
        {
            return deriveFailure;
        }

        if (Input.Level == 1)
        {
            Input.ParentId = null;
        }

        var optionFailure = await LoadOptionsAsync(cancellationToken);
        if (optionFailure is not null)
        {
            return optionFailure;
        }

        await ValidateParentAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await regionClient.CreateAsync(Input, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = localizer["Common.ValidationFailed"].Value;
            return Page();
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ErrorMessage = localizer["GeoDivisions.Form.UniqueConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.Create.SaveFailed"].Value;
            return Page();
        }

        var created = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (created is null)
        {
            ErrorMessage = localizer["GeoDivisions.Create.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["GeoDivisions.Create.Created"].Value;
        return RedirectToPage("/GeoDivisions/Detail", new { id = created.Id });
    }

    private async Task<IActionResult?> DeriveLevelFromParentAsync(CancellationToken cancellationToken)
    {
        if (!Input.ParentId.HasValue)
        {
            Input.Level = 1;
            SelectedParentLabel = string.Empty;
            return null;
        }

        using var response = await regionClient.GetAsync(Input.ParentId.Value, cancellationToken);
        var statusFailure = HandleStatus(response.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!response.IsSuccessStatusCode)
        {
            Input.ParentId = null;
            Input.Level = 1;
            SelectedParentLabel = string.Empty;
            ErrorMessage = localizer["Validation.GeoDivision.ParentInvalid"].Value;
            return null;
        }

        var parent = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (parent is null || parent.Level > MaxParentLevel)
        {
            Input.ParentId = null;
            Input.Level = 1;
            SelectedParentLabel = string.Empty;
            ErrorMessage = localizer["Validation.GeoDivision.ParentInvalid"].Value;
            return null;
        }

        Input.Level = parent.Level + 1;
        Input.CountryCode = parent.CountryCode;
        SelectedParentLabel = $"{parent.Name} ({parent.Code})";
        return null;
    }

    private async Task<IActionResult?> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var kindFailure = await LoadKindOptionsAsync(cancellationToken);
        if (kindFailure is not null)
        {
            return kindFailure;
        }

        using var countriesResponse = await regionClient.ListAsync(
            1, 250, level: 1, null, null, null, cancellationToken);
        var statusFailure = HandleStatus(countriesResponse.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.LoadCountriesFailed"].Value;
            return null;
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionView>>(cancellationToken);
        Countries = countriesPage?.Items ?? [];
        return null;
    }

    private async Task ValidateParentAsync(CancellationToken cancellationToken)
    {
        if (Input.Level == 1)
        {
            return;
        }

        if (!Input.ParentId.HasValue)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentRequired"].Value);
            return;
        }

        using var response = await regionClient.GetAsync(Input.ParentId.Value, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
            return;
        }

        var parent = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (parent is null
            || parent.Level > MaxParentLevel
            || !string.Equals(parent.CountryCode, Input.CountryCode, StringComparison.OrdinalIgnoreCase)
            || parent.Level + 1 != Input.Level)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
        }
    }

    /// <summary>
    /// 按国家分域加载 Kind 选项（该国家未维护时回退通用域）。
    /// 返回 (选项列表, 是否加载失败)。
    /// </summary>
    private async Task<(List<ReferenceEnumView> Items, bool LoadFailed)> LoadKindItemsAsync(
        string? countryCode, CancellationToken cancellationToken)
    {
        var category = ReferenceEnumClient.GeoDivisionKindCategoryFor(countryCode);
        using var response = await enumClient.ListAsync(
            category,
            pageSize: 200,
            cancellationToken: cancellationToken);
        var statusFailure = HandleStatus(response.StatusCode);
        if (statusFailure is not null)
        {
            return ([], true);
        }

        if (!response.IsSuccessStatusCode)
        {
            return ([], true);
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        var items = (page?.Items ?? [])
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code)
            .ToList();

        if (items.Count == 0
            && !string.Equals(category, ReferenceEnumClient.GeoDivisionKindCategory, StringComparison.Ordinal))
        {
            using var fallback = await enumClient.ListAsync(
                ReferenceEnumClient.GeoDivisionKindCategory,
                pageSize: 200,
                cancellationToken: cancellationToken);
            statusFailure = HandleStatus(fallback.StatusCode);
            if (statusFailure is not null)
            {
                return ([], true);
            }

            if (fallback.IsSuccessStatusCode)
            {
                var fallbackPage = await fallback.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
                items = (fallbackPage?.Items ?? [])
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Code)
                    .ToList();
            }
        }

        return (items, false);
    }

    /// <summary>国家变更时由 site.js 调用：返回该国家分域的 Kind 选项（按层级过滤 country）。</summary>
    public async Task<IActionResult> OnGetKindOptionsAsync(
        string? countryCode, int level = 2, CancellationToken cancellationToken = default)
    {
        var (items, loadFailed) = await LoadKindItemsAsync(countryCode, cancellationToken);
        var filtered = loadFailed
            ? []
            : (level == 1
                ? items.Where(item => item.Code == "country").ToList()
                : items.Where(item => item.Code != "country").ToList());

        return new JsonResult(filtered.Select(item => new { value = item.Code, text = item.Name }).ToList());
    }

    private async Task<IActionResult?> LoadKindOptionsAsync(CancellationToken cancellationToken)
    {
        var (items, loadFailed) = await LoadKindItemsAsync(Input.CountryCode, cancellationToken);
        if (loadFailed)
        {
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            KindOptions = [];
            return null;
        }

        // 国家行的 Kind 固定为 country；其余层级不允许选 country。
        items = Input.Level == 1
            ? items.Where(item => item.Code == "country").ToList()
            : items.Where(item => item.Code != "country").ToList();

        // 预置“自动推导”空选项：留空提交时由 API 按国家与层级推导默认 Kind。
        KindOptions = new[] { new SelectListItem(localizer["GeoDivisions.Form.KindAuto"].Value, "") }
            .Concat(items.Select(item => new SelectListItem(item.Name, item.Code)))
            .ToList();

        return null;
    }

    private static IReadOnlyList<ParentTreeSelectItemDto> MapChildren(
        IEnumerable<GeoDivisionChildView> children,
        Guid? selectedId,
        Guid? excludeId,
        int maxParentLevel,
        int? selectableLevel)
    {
        var expandUntil = selectableLevel ?? maxParentLevel;
        return children
            .Where(child => excludeId is null || child.Id != excludeId.Value)
            .Select(child => ToDto(
                child.Id,
                child.Name,
                child.Code,
                child.Level,
                selectedId,
                maxParentLevel,
                selectableLevel,
                hasChildren: child.Level < expandUntil
                    && child.ChildCount > child.SameNameChildCount))
            .ToList();
    }

    private static ParentTreeSelectItemDto ToDto(
        Guid id,
        string name,
        string code,
        int level,
        Guid? selectedId,
        int maxParentLevel,
        int? selectableLevel,
        bool hasChildren)
    {
        var selectable = selectableLevel is int required
            ? level == required
            : level <= maxParentLevel;

        return new ParentTreeSelectItemDto(
            id,
            $"{name} ({code})",
            level,
            selectable,
            hasChildren,
            selectedId == id);
    }

    private IActionResult? HandleStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => Challenge(),
        HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
        _ => null
    };
}
