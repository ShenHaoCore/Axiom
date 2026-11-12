// 编辑行政区名称、译文相关字段与地理信息；Code / Depth / Type 等业务标识保持只读。
// 按 (country, code) 复合业务键寻址。
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

    public string? EditCountryCode { get; private set; }

    public string? EditCode { get; private set; }

    public async Task<IActionResult> OnGetAsync(string country, string code, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(country, code, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = RegionInput.From(Region);
        var optionFailure = await LoadParentOptionsAsync(cancellationToken);
        return optionFailure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(string country, string code, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(country, code, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Region is null)
        {
            return Page();
        }

        // 标识字段以服务端实体为准，防止隐藏字段被篡改。
        Input.Depth = Region.Depth;
        Input.CountryCode = Region.CountryCode;
        Input.Code = Region.Code;
        Input.ParentCode = Region.ParentCode;
        Input.Type = Region.Type;

        var optionFailure = await LoadParentOptionsAsync(cancellationToken);
        if (optionFailure is not null)
        {
            return optionFailure;
        }

        using var response = await regionClient.UpdateAsync(country, code, Input, cancellationToken);
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

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.Edit.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Regions.Edit.Updated"].Value;
        return RedirectToPage("/Regions/Detail", new { country, code });
    }

    private async Task<IActionResult?> LoadRegionAsync(string country, string code, CancellationToken cancellationToken)
    {
        using var response = await regionClient.GetAsync(country, code, cancellationToken);
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
        else
        {
            EditCountryCode = Region.CountryCode;
            EditCode = Region.Code;
        }

        return null;
    }

    private async Task<IActionResult?> LoadParentOptionsAsync(CancellationToken cancellationToken)
    {
        ParentOptions = [];
        if (Region is null || Region.Depth == 0)
        {
            return null;
        }

        using var response = await regionClient.GetTreeAsync(Region.CountryCode, depth: Region.Depth - 1, cancellationToken);
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
            ParentOptions = BuildParentOptions(tree, Region.Depth - 1, Region.Code);
        }

        return null;
    }

    private static IReadOnlyList<SelectListItem> BuildParentOptions(
        RegionTreeView tree,
        int parentDepth,
        string editedCode)
    {
        var options = new List<SelectListItem>();

        void Visit(RegionTreeView node, int level, bool inEditedBranch)
        {
            var excluded = inEditedBranch || node.Code == editedCode;
            if (!excluded && node.Depth == parentDepth)
            {
                options.Add(new SelectListItem(
                    $"{new string('—', level)} {node.Name} ({node.Code})",
                    node.Code));
            }

            foreach (var child in node.Children)
            {
                Visit(child, level + 1, excluded || child.Code == editedCode);
            }
        }

        Visit(tree, 0, false);
        return options;
    }
}
