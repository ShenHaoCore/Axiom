// 编辑行政区名称、代码、地理信息和父级；层级与国家等标识字段保持只读。
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

public sealed class EditModel(
    GeoDivisionClient regionClient,
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public GeoDivisionInput Input { get; set; } = new();

    public GeoDivisionView? GeoDivision { get; private set; }

    public IReadOnlyList<SelectListItem> ParentOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> KindOptions { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = GeoDivisionInput.From(GeoDivision);
        var kindFailure = await LoadKindOptionsAsync(cancellationToken);
        if (kindFailure is not null)
        {
            return kindFailure;
        }

        var optionFailure = await LoadParentOptionsAsync(cancellationToken);
        return optionFailure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (GeoDivision is null)
        {
            return Page();
        }

        // 标识字段以服务端实体为准，防止隐藏字段被篡改。
        Input.Level = GeoDivision.Level;
        Input.CountryCode = GeoDivision.CountryCode;
        Input.SubdivisionCode = GeoDivision.SubdivisionCode;
        if (Input.Level == 1)
        {
            Input.ParentId = null;
        }

        var kindFailure = await LoadKindOptionsAsync(cancellationToken);
        if (kindFailure is not null)
        {
            return kindFailure;
        }

        var optionFailure = await LoadParentOptionsAsync(cancellationToken);
        if (optionFailure is not null)
        {
            return optionFailure;
        }

        ValidateParent();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await regionClient.UpdateAsync(id, Input, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
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
            ErrorMessage = localizer["GeoDivisions.Edit.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["GeoDivisions.Edit.Updated"].Value;
        return RedirectToPage("/GeoDivisions/Detail", new { id });
    }

    private async Task<IActionResult?> LoadRegionAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await regionClient.GetAsync(id, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.Edit.LoadFailed"].Value;
            return null;
        }

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (GeoDivision is null)
        {
            ErrorMessage = localizer["GeoDivisions.Edit.LoadFailed"].Value;
        }

        return null;
    }

    private async Task<IActionResult?> LoadKindOptionsAsync(CancellationToken cancellationToken)
    {
        using var response = await enumClient.ListAsync(
            ReferenceEnumClient.GeoDivisionKindCategory,
            pageSize: 200,
            cancellationToken: cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
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

        KindOptions = items
            .Select(item => new SelectListItem(item.Name, item.Code))
            .ToList();

        if (!string.IsNullOrWhiteSpace(Input.Kind)
            && KindOptions.All(option => !string.Equals(option.Value, Input.Kind, StringComparison.OrdinalIgnoreCase)))
        {
            KindOptions = KindOptions
                .Append(new SelectListItem(Input.Kind, Input.Kind))
                .ToList();
        }

        return null;
    }

    private async Task<IActionResult?> LoadParentOptionsAsync(CancellationToken cancellationToken)
    {
        ParentOptions = [];
        if (GeoDivision is null || GeoDivision.Level == 1)
        {
            return null;
        }

        using var response = await regionClient.GetTreeAsync(GeoDivision.CountryCode, depth: 4, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.LoadTreeFailed", GeoDivision.CountryCode].Value;
            return null;
        }

        var tree = await response.Content.ReadFromJsonAsync<GeoDivisionTreeView>(cancellationToken);
        if (tree is not null)
        {
            ParentOptions = BuildParentOptions(tree, GeoDivision.Level - 1, GeoDivision.Id);
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

        if (ParentOptions.All(option => option.Value != Input.ParentId.Value.ToString()))
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
        }
    }

    private static IReadOnlyList<SelectListItem> BuildParentOptions(
        GeoDivisionTreeView tree,
        int parentLevel,
        Guid editedId)
    {
        var options = new List<SelectListItem>();

        void Visit(GeoDivisionTreeView node, int depth, bool inEditedBranch)
        {
            var excluded = inEditedBranch || node.Id == editedId;
            if (!excluded && node.Level == parentLevel)
            {
                options.Add(new SelectListItem(
                    $"{new string('—', depth)} {node.Name} ({node.Code})",
                    node.Id.ToString()));
            }

            foreach (var child in node.Children)
            {
                Visit(child, depth + 1, excluded);
            }
        }

        Visit(tree, 0, false);
        return options;
    }
}
