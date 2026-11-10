// 编辑行政区名称、代码、地理信息和父级；层级与国家等标识字段保持只读。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Regions;

public sealed class EditModel(
    RegionClient regionClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public RegionInput Input { get; set; } = new();

    public RegionView? Region { get; private set; }

    public IReadOnlyList<SelectListItem> ParentOptions { get; private set; } = [];

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

        Input = RegionInput.From(Region);
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

        if (Region is null)
        {
            return Page();
        }

        // 标识字段以服务端实体为准，防止隐藏字段被篡改。
        Input.Level = Region.Level;
        Input.CountryCode = Region.CountryCode;
        Input.Admin1Code = Region.Admin1Code;
        if (Input.Level == 1)
        {
            Input.ParentId = null;
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
            ErrorMessage = localizer["Regions.Form.UniqueConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.Edit.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Regions.Edit.Updated"].Value;
        return RedirectToPage("/Regions/Detail", new { id });
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
            ErrorMessage = localizer["Regions.Edit.LoadFailed"].Value;
            return null;
        }

        Region = await response.Content.ReadFromJsonAsync<RegionView>(cancellationToken);
        if (Region is null)
        {
            ErrorMessage = localizer["Regions.Edit.LoadFailed"].Value;
        }

        return null;
    }

    private async Task<IActionResult?> LoadParentOptionsAsync(CancellationToken cancellationToken)
    {
        ParentOptions = [];
        if (Region is null || Region.Level == 1)
        {
            return null;
        }

        using var response = await regionClient.GetTreeAsync(Region.CountryCode, depth: 3, cancellationToken);
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
            ErrorMessage = localizer["Regions.LoadTreeFailed", Region.CountryCode].Value;
            return null;
        }

        var tree = await response.Content.ReadFromJsonAsync<RegionTreeView>(cancellationToken);
        if (tree is not null)
        {
            ParentOptions = BuildParentOptions(tree, Region.Level - 1, Region.Id);
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

    private static IReadOnlyList<SelectListItem> BuildParentOptions(
        RegionTreeView tree,
        int parentLevel,
        Guid editedId)
    {
        var options = new List<SelectListItem>();

        void Visit(RegionTreeView node, int depth, bool inEditedBranch)
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
