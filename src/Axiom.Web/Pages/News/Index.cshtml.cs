// 提供新闻列表、状态筛选和入口跳转。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.News;

public sealed class IndexModel(NewsClient newsClient) : PageModel
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
            ErrorMessage = "无法加载新闻列表，请稍后重试。";
            return Page();
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<NewsArticleView>>(cancellationToken);
        Articles = page?.Items ?? [];
        TotalCount = page?.TotalCount ?? 0;
        return Page();
    }
}
