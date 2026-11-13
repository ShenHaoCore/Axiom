// 新增行政区：层级由父级推导（无父级为 1），并按国家加载父级与 Kind。
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
    private const int MaxParentLevel = 3;

    [BindProperty]
    public GeoDivisionInput Input { get; set; } = new();

    public IReadOnlyList<GeoDivisionView> Countries { get; private set; } = [];

    public IReadOnlyList<ParentTreeSelectNode> ParentTree { get; private set; } = [];

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
        }

        var failure = await LoadOptionsAsync(cancellationToken);
        return failure ?? Page();
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

        ValidateParent();
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
            ErrorMessage = localizer["Validation.GeoDivision.ParentInvalid"].Value;
            return null;
        }

        var parent = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (parent is null || parent.Level > MaxParentLevel)
        {
            Input.ParentId = null;
            Input.Level = 1;
            ErrorMessage = localizer["Validation.GeoDivision.ParentInvalid"].Value;
            return null;
        }

        Input.Level = parent.Level + 1;
        Input.CountryCode = parent.CountryCode;
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

        ParentTree = [];
        if (string.IsNullOrWhiteSpace(Input.CountryCode))
        {
            return null;
        }

        using var treeResponse = await regionClient.GetTreeAsync(Input.CountryCode, depth: 4, cancellationToken);
        statusFailure = HandleStatus(treeResponse.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        // 新建国家时尚无根节点，树接口会 404：L1 静默空父级树，不挡表单。
        if (!treeResponse.IsSuccessStatusCode)
        {
            if (Input.Level > 1)
            {
                ErrorMessage = localizer["GeoDivisions.LoadTreeFailed", Input.CountryCode].Value;
            }

            return null;
        }

        var tree = await treeResponse.Content.ReadFromJsonAsync<GeoDivisionTreeView>(cancellationToken);
        if (tree is not null)
        {
            ParentTree = ParentTreeSelectViewModel.Build(
                tree,
                Input.ParentId,
                node => node.Level <= MaxParentLevel);
        }

        return null;
    }

    private void ValidateParent()
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

        if (!ParentTreeSelectViewModel.ContainsSelectable(ParentTree, Input.ParentId.Value))
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
        }
    }

    private async Task<IActionResult?> LoadKindOptionsAsync(CancellationToken cancellationToken)
    {
        // Kind 选项按所选国家的分域字典；该国家未维护时回退通用域。
        var category = ReferenceEnumClient.GeoDivisionKindCategoryFor(Input.CountryCode);
        using var response = await enumClient.ListAsync(
            category,
            pageSize: 200,
            cancellationToken: cancellationToken);
        var statusFailure = HandleStatus(response.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            KindOptions = [];
            return null;
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
                return statusFailure;
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

        KindOptions = items
            .Select(item => new SelectListItem(item.Name, item.Code))
            .ToList();

        if (string.IsNullOrWhiteSpace(Input.Kind) || KindOptions.All(option => option.Value != Input.Kind))
        {
            Input.Kind = KindOptions.FirstOrDefault()?.Value ?? "other";
        }

        return null;
    }

    private IActionResult? HandleStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => Challenge(),
        HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
        _ => null
    };
}
