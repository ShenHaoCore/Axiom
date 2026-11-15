// 提供独立的新增机场页面，并透传网关的认证与校验结果。
using System.Net;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Airports;

public sealed class CreateModel(
    ReferenceDataClient referenceDataClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public AirportInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await referenceDataClient.CreateAirportAsync(Input, cancellationToken);
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
            ErrorMessage = response.StatusCode switch
            {
                HttpStatusCode.Conflict => localizer["Airports.CodeConflict"].Value,
                HttpStatusCode.BadRequest => localizer["Common.ValidationFailed"].Value,
                _ => localizer["Airports.CreateFailed"].Value
            };
            return Page();
        }

        StatusMessage = localizer["Airports.Created"].Value;
        return RedirectToPage("/Index");
    }
}
