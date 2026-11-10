// 新增行政区，并按国家和层级加载合法的父级选项。
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

namespace Axiom.Web.Pages.Regions;

public sealed class CreateModel(
    RegionClient regionClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public RegionInput Input { get; set; } = new();

    public IReadOnlyList<RegionView> Countries { get; private set; } = [];

    public IReadOnlyList<SelectListItem> ParentOptions { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? countryCode,
        int? level,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        Input.CountryCode = string.IsNullOrWhiteSpace(countryCode)
            ? Input.CountryCode
            : countryCode.Trim().ToUpperInvariant();
        Input.Level = level is >= 1 and <= 3 ? level.Value : Input.Level;
        Input.ParentId = parentId;

        var failure = await LoadOptionsAsync(cancellationToken);
        return failure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        Input.CountryCode = Input.CountryCode?.Trim().ToUpperInvariant();
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
            ErrorMessage = localizer["Regions.Form.UniqueConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.Create.SaveFailed"].Value;
            return Page();
        }

        var created = await response.Content.ReadFromJsonAsync<RegionView>(cancellationToken);
        if (created is null)
        {
            ErrorMessage = localizer["Regions.Create.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Regions.Create.Created"].Value;
        return RedirectToPage("/Regions/Detail", new { id = created.Id });
    }

    private async Task<IActionResult?> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        using var countriesResponse = await regionClient.ListAsync(
            1, 250, level: 1, null, null, null, cancellationToken);
        var statusFailure = HandleStatus(countriesResponse.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!countriesResponse.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.LoadCountriesFailed"].Value;
            return null;
        }

        var countriesPage = await countriesResponse.Content.ReadFromJsonAsync<PagedResponse<RegionView>>(cancellationToken);
        Countries = countriesPage?.Items ?? [];
        if (string.IsNullOrWhiteSpace(Input.CountryCode))
        {
            Input.CountryCode = Countries.FirstOrDefault()?.CountryCode;
        }

        ParentOptions = [];
        if (Input.Level == 1 || string.IsNullOrWhiteSpace(Input.CountryCode))
        {
            return null;
        }

        using var treeResponse = await regionClient.GetTreeAsync(Input.CountryCode, depth: 3, cancellationToken);
        statusFailure = HandleStatus(treeResponse.StatusCode);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!treeResponse.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.LoadTreeFailed", Input.CountryCode].Value;
            return null;
        }

        var tree = await treeResponse.Content.ReadFromJsonAsync<RegionTreeView>(cancellationToken);
        if (tree is not null)
        {
            ParentOptions = BuildParentOptions(tree, Input.Level - 1);
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
            ModelState.AddModelError("Input.ParentId", localizer["Validation.Region.ParentRequired"].Value);
            return;
        }

        if (ParentOptions.All(option => option.Value != Input.ParentId.Value.ToString()))
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.Region.ParentInvalid"].Value);
        }
    }

    private static IReadOnlyList<SelectListItem> BuildParentOptions(RegionTreeView tree, int parentLevel)
    {
        var options = new List<SelectListItem>();

        void Visit(RegionTreeView node, int depth)
        {
            if (node.Level == parentLevel)
            {
                options.Add(new SelectListItem(
                    $"{new string('—', depth)} {node.Name} ({node.Code})",
                    node.Id.ToString()));
            }

            foreach (var child in node.Children)
            {
                Visit(child, depth + 1);
            }
        }

        Visit(tree, 0);
        return options;
    }

    private IActionResult? HandleStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => Challenge(),
        HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
        _ => null
    };
}
