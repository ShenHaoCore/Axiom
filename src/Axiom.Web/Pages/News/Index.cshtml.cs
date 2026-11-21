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
    private const int PAGE_SIZE = 20;

    public IReadOnlyList<NewsArticleView> Articles { get; private set; } = [];

    public int TotalCount { get; private set; }

    /// <summary>当前页码与总页数，供分页控件渲染。</summary>
    public int CurrentPage { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public string? StatusFilter { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        StatusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var currentPage = Math.Max(page, 1);
        using var response = await newsClient.ListAsync(StatusFilter, currentPage, PAGE_SIZE, cancellationToken);

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

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<NewsArticleView>>(cancellationToken);
        Articles = paged?.Items ?? [];
        TotalCount = paged?.TotalCount ?? 0;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PAGE_SIZE));
        CurrentPage = Math.Min(currentPage, TotalPages);
        return Page();
    }
}
