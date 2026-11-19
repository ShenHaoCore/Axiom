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

        // 搜索：跨已加载树，直接按名称/代码查可选父级；未指定国家时跨全部国家。
        if (keyword is not null && parentId is null)
        {
            var cc = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();

            using var searchResponse = await regionClient.ListAsync(
                1, 50, level: null, cc, null, keyword, cancellationToken);
            var searchStatus = this.HandleAuthStatus(searchResponse.StatusCode);
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
                    hasChildren: false,
                    item.CountryCode,
                    item.SubdivisionCode))
                .ToList();

            return new JsonResult(hits);
        }

        if (parentId is Guid id)
        {
            using var childrenResponse = await regionClient.GetChildrenAsync(id, cancellationToken);
            var childrenStatus = this.HandleAuthStatus(childrenResponse.StatusCode);
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

        using var rootsResponse = await regionClient.ListAsync(
            1, 50, level: 1, country, null, null, cancellationToken);
        var rootsStatus = this.HandleAuthStatus(rootsResponse.StatusCode);
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
                hasChildren: item.Level < (selectableLevel ?? maxParentLevel),
                item.CountryCode,
                item.SubdivisionCode))
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
        var authFailure = this.HandleAuthStatus(response.StatusCode);
        if (authFailure is not null)
        {
            return authFailure;
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
        var statusFailure = this.HandleAuthStatus(response.StatusCode);
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
        // 一级区划码：二级行由用户填写；三级及以下与父级链一致（API 侧按 L2 祖先最终校验）。
        Input.SubdivisionCode = parent.Level == 1
            ? Input.SubdivisionCode?.Trim()
            : parent.SubdivisionCode;
        SelectedParentLabel = $"{parent.Name} ({parent.Code})";
        return null;
    }

    private async Task<IActionResult?> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        await LoadKindOptionsAsync(cancellationToken);

        using var countriesResponse = await regionClient.ListAsync(
            1, 250, level: 1, null, null, null, cancellationToken);
        var statusFailure = this.HandleAuthStatus(countriesResponse.StatusCode);
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
            || parent.Level + 1 != Input.Level)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
        }
    }

    /// <summary>国家变更时由 site.js 调用：返回该国家分域的 Kind 选项（按层级过滤 country）。</summary>
    public async Task<IActionResult> OnGetKindOptionsAsync(
        string? countryCode, int level = 2, CancellationToken cancellationToken = default)
    {
        var (items, status) = await enumClient.ListGeoDivisionKindsAsync(
            level == 1 ? null : countryCode, cancellationToken);
        var options = status == HttpStatusCode.OK
            ? GeoDivisionKindOptions.ToSelectList(items, level)
            : [];

        return new JsonResult(options.Select(option => new { value = option.Value, text = option.Text }).ToList());
    }

    private async Task LoadKindOptionsAsync(CancellationToken cancellationToken)
    {
        // 国家行（L=1）固定读通用域：country 仅在通用域维护。
        var (items, status) = await enumClient.ListGeoDivisionKindsAsync(
            Input.Level == 1 ? null : Input.CountryCode, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            KindOptions = [];
            return;
        }

        KindOptions = GeoDivisionKindOptions.ToSelectList(items, Input.Level);
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
                    && child.ChildCount > child.SameNameChildCount,
                child.CountryCode,
                child.SubdivisionCode))
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
        bool hasChildren,
        string countryCode,
        string? subdivisionCode)
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
            selectedId == id,
            countryCode,
            subdivisionCode);
    }
}
