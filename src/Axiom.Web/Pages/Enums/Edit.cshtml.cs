// 编辑枚举字典显示名与排序；Category/Code 只读。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Enums;

public sealed class EditModel(
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public ReferenceEnumInput Input { get; set; } = new();

    public ReferenceEnumView? Item { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = ReferenceEnumInput.From(Item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Item is null)
        {
            return Page();
        }

        Input.Category = Item.Category;
        Input.Code = Item.Code;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await enumClient.UpdateAsync(id, Input, cancellationToken);
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
            ErrorMessage = localizer["Enums.EditFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Enums.Updated"].Value;
        return RedirectToPage("/Enums/Index", new { category = Item.Category });
    }

    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await enumClient.GetAsync(id, cancellationToken);
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
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            return Page();
        }

        Item = await response.Content.ReadFromJsonAsync<ReferenceEnumView>(cancellationToken);
        return Item is null ? NotFound() : null;
    }
}
