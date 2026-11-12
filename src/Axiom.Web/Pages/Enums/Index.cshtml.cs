// 枚举字典列表：按 Category 筛选，支持删除。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Enums;

public sealed class IndexModel(
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    public IReadOnlyList<ReferenceEnumView> Items { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        using var response = await enumClient.ListAsync(Category, cancellationToken: cancellationToken);
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
            return Page();
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        Items = page?.Items ?? [];
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await enumClient.DeleteAsync(id, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            StatusMessage = localizer["Enums.DeleteInUse"].Value;
            return RedirectToPage(new { Category });
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            StatusMessage = localizer["Enums.NotFound"].Value;
            return RedirectToPage(new { Category });
        }

        StatusMessage = response.IsSuccessStatusCode
            ? localizer["Enums.Deleted"].Value
            : localizer["Enums.DeleteFailed"].Value;
        return RedirectToPage(new { Category });
    }
}
