// 展示机场详情，并使用 ETag 完成乐观并发更新和删除。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Airports;

public sealed class EditModel(
    ReferenceDataClient referenceDataClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public AirportResponse? Airport { get; private set; }

    public Guid AirportId { get; private set; }

    [BindProperty]
    public AirportInput Input { get; set; } = new();

    [BindProperty]
    public string? ETag { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AirportId = id;
        var failure = await LoadAirportAsync(id, cancellationToken);
        return failure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AirportId = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ETag))
        {
            ErrorMessage = localizer["Common.MissingETag"].Value;
            return Page();
        }

        using var response = await referenceDataClient.UpdateAirportAsync(id, ETag, Input, cancellationToken);
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
            ErrorMessage = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => localizer["Common.ValidationFailed"].Value,
                HttpStatusCode.Conflict => localizer["Common.ConcurrencyConflict"].Value,
                HttpStatusCode.PreconditionRequired => localizer["Common.MissingETag"].Value,
                _ => localizer["Airports.Edit.SaveFailed"].Value
            };
            return Page();
        }

        StatusMessage = localizer["Airports.Edit.Updated"].Value;
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AirportId = id;
        if (string.IsNullOrWhiteSpace(ETag))
        {
            ErrorMessage = localizer["Common.MissingETag"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        using var response = await referenceDataClient.DeleteAirportAsync(id, ETag, cancellationToken);
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
            ErrorMessage = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => localizer["Common.ValidationFailed"].Value,
                HttpStatusCode.Conflict => localizer["Common.ConcurrencyConflict"].Value,
                HttpStatusCode.PreconditionRequired => localizer["Common.MissingETag"].Value,
                _ => localizer["Airports.Edit.DeleteFailed"].Value
            };
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        StatusMessage = localizer["Airports.Edit.Deleted"].Value;
        return RedirectToPage("/Index");
    }

    private async Task<IActionResult> ReloadAfterDeleteFailureAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var failure = await LoadAirportAsync(id, cancellationToken);
        return failure ?? Page();
    }

    private async Task<IActionResult?> LoadAirportAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await referenceDataClient.GetAirportAsync(id, cancellationToken);
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
            ErrorMessage = localizer["Airports.Edit.LoadFailed"].Value;
            return null;
        }

        Airport = await response.Content.ReadFromJsonAsync<AirportResponse>(cancellationToken);
        AirportId = id;
        ETag = response.Headers.ETag?.Tag;
        Input = AirportInput.From(Airport);
        return null;
    }
}
