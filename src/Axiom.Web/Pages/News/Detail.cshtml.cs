// 展示单条新闻详情，并支持删除。
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.News;

public sealed class DetailModel(NewsClient newsClient) : PageModel
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
            ErrorMessage = "缺少并发令牌，请刷新页面后重试。";
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
            ErrorMessage = "新闻已被其他请求修改，请刷新后重试。";
            var reloadFailure = await LoadAsync(ArticleId, cancellationToken);
            return reloadFailure ?? Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "删除新闻失败，请稍后重试。";
            var reloadFailure = await LoadAsync(ArticleId, cancellationToken);
            return reloadFailure ?? Page();
        }

        StatusMessage = "新闻已删除。";
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
            ErrorMessage = "无法加载新闻详情，请稍后重试。";
            return null;
        }

        Article = await response.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken);
        ArticleId = id;
        ETag = response.Headers.ETag?.Tag;
        return null;
    }
}
