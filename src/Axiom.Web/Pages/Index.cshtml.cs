// 提供机场列表，并透传网关的认证与加载结果。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages;

public sealed class IndexModel(
    ReferenceDataClient referenceDataClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<AirportResponse> Airports { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default) =>
        PageWithAirportsAsync(cancellationToken);

    private async Task<IActionResult> PageWithAirportsAsync(CancellationToken cancellationToken)
    {
        var failure = await LoadAirportsAsync(cancellationToken);
        return failure ?? Page();
    }

    private async Task<IActionResult?> LoadAirportsAsync(CancellationToken cancellationToken)
    {
        using var response = await referenceDataClient.GetAirportsAsync(cancellationToken);
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
            ErrorMessage = localizer["Airports.LoadFailed"].Value;
            return null;
        }

        var airports = await response.Content.ReadFromJsonAsync<PagedResponse<AirportResponse>>(cancellationToken);
        Airports = airports?.Items ?? [];
        return null;
    }
}
