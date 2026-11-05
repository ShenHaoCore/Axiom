// 提供机场列表和新增操作，并透传网关的认证与校验结果。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages;

public sealed class IndexModel(ReferenceDataClient referenceDataClient) : PageModel
{
    public IReadOnlyList<AirportResponse> Airports { get; private set; } = [];

    [BindProperty]
    public AirportInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default) =>
        PageWithAirportsAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return await PageWithAirportsAsync(cancellationToken);
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
                HttpStatusCode.Conflict => "机场代码已存在。",
                HttpStatusCode.BadRequest => "提交内容未通过服务端校验，请检查后重试。",
                _ => "新增机场失败，请稍后重试。"
            };
            return await PageWithAirportsAsync(cancellationToken);
        }

        StatusMessage = "机场已新增。";
        return RedirectToPage();
    }

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
            ErrorMessage = "无法加载机场列表，请稍后重试。";
            return null;
        }

        var airports = await response.Content.ReadFromJsonAsync<PagedResponse<AirportResponse>>(cancellationToken);
        Airports = airports?.Items ?? [];
        return null;
    }
}
