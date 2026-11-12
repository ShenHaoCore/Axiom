// 新增枚举字典项。
using System.Net;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Enums;

public sealed class CreateModel(
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public ReferenceEnumInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            Input.Category = category.Trim().ToLowerInvariant();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        Input.Category = Input.Category?.Trim().ToLowerInvariant();
        Input.Code = Input.Code?.Trim().ToLowerInvariant();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await enumClient.CreateAsync(Input, cancellationToken);
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
            ErrorMessage = localizer["Enums.CodeConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Enums.CreateFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Enums.Created"].Value;
        return RedirectToPage("/Enums/Index", new { category = Input.Category });
    }
}
