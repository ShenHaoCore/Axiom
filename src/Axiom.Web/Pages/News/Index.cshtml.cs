// 提供新闻列表、状态筛选和入口跳转。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.News;

public sealed class IndexModel(
    NewsClient newsClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<NewsArticleView> Articles { get; private set; } = [];

    public int TotalCount { get; private set; }

    public string? StatusFilter { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? status, CancellationToken cancellationToken = default)
    {
        StatusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        using var response = await newsClient.ListAsync(StatusFilter, 1, cancellationToken);

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
            ErrorMessage = localizer["News.LoadFailed"].Value;
            return Page();
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<NewsArticleView>>(cancellationToken);
        Articles = page?.Items ?? [];
        TotalCount = page?.TotalCount ?? 0;
        return Page();
    }
}
