// 展示单条新闻详情，并支持删除。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.News;

public sealed class DetailModel(
    NewsClient newsClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public NewsArticleView? Article { get; private set; }

    [BindProperty]
    public Guid ArticleId { get; set; }

    [BindProperty]
    public string? ETag { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadAsync(id, cancellationToken);
        return failure ?? Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (ETag is null)
        {
            ErrorMessage = localizer["Common.MissingETag"].Value;
            var reloadFailure = await LoadAsync(ArticleId, cancellationToken);
            return reloadFailure ?? Page();
        }

        using var response = await newsClient.DeleteAsync(ArticleId, ETag, cancellationToken);
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
            ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
            var reloadFailure = await LoadAsync(ArticleId, cancellationToken);
            return reloadFailure ?? Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["News.Detail.DeleteFailed"].Value;
            var reloadFailure = await LoadAsync(ArticleId, cancellationToken);
            return reloadFailure ?? Page();
        }

        StatusMessage = localizer["News.Detail.Deleted"].Value;
        return RedirectToPage("/News/Index");
    }

    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await newsClient.GetAsync(id, cancellationToken);
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
            ErrorMessage = localizer["News.Detail.LoadFailed"].Value;
            return null;
        }

        Article = await response.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken);
        ArticleId = id;
        ETag = response.Headers.ETag?.Tag;
        return null;
    }
}
