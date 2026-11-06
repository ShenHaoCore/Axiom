// 行政区详情页。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.Regions;

public sealed class DetailModel(RegionClient regionClient) : PageModel
{
    public RegionView? Region { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await regionClient.GetAsync(id, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Challenge(),
            HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            HttpStatusCode.NotFound => NotFound(),
            { } when !response.IsSuccessStatusCode => PageWithError("无法加载行政区详情。"),
            _ => await LoadSuccessAsync(response, cancellationToken)
        };
    }

    private async Task<IActionResult> LoadSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Region = await response.Content.ReadFromJsonAsync<RegionView>(cancellationToken);
        return Page();
    }

    private IActionResult PageWithError(string message)
    {
        ErrorMessage = message;
        return Page();
    }
}
