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

    // [TempData]：删除失败经 RedirectToPage 返回列表，错误信息必须能跨请求存活，
    // 否则失败分支会静默无提示（这是“失败当成功播报”问题的另一半根因）。
    [TempData]
    public string? ErrorMessage { get; set; }

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
        // 删除需要 If-Match：先取当前条目与其 ETag，再删除。
        string? eTag;
        using (var getResponse = await enumClient.GetAsync(id, cancellationToken))
        {
            if (getResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            if (getResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (getResponse.StatusCode == HttpStatusCode.NotFound)
            {
                ErrorMessage = localizer["Enums.NotFound"].Value;
                return RedirectToPage(new { Category });
            }

            eTag = getResponse.Headers.ETag?.Tag;
        }

        using var response = await enumClient.DeleteAsync(id, eTag ?? string.Empty, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Challenge();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // 失败一律走 ErrorMessage（红色、手动关闭）：失败混进成功通道会让用户误以为已删除。
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ErrorMessage = localizer["Enums.DeleteInUse"].Value;
            return RedirectToPage(new { Category });
        }

        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
            return RedirectToPage(new { Category });
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            ErrorMessage = localizer["Enums.NotFound"].Value;
            return RedirectToPage(new { Category });
        }

        if (response.IsSuccessStatusCode)
        {
            StatusMessage = localizer["Enums.Deleted"].Value;
        }
        else
        {
            ErrorMessage = localizer["Enums.DeleteFailed"].Value;
        }

        return RedirectToPage(new { Category });
    }
}
