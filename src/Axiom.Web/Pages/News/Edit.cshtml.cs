// 新增或编辑新闻，编辑时携带 ETag 完成乐观并发更新。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.News;

public sealed class EditModel(
    NewsClient newsClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public NewsInput Input { get; set; } = new();

    [BindProperty]
    public Guid? ArticleId { get; set; }

    [BindProperty]
    public string? ETag { get; set; }

    public bool IsEdit => ArticleId is not null;

    public string? ErrorMessage { get; private set; }

    /// <summary>412 并发冲突时置位：错误区渲染“查看最新内容”链接（输入已保留）。</summary>
    public bool ShowLatestLink { get; private set; }

    public string? LatestUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        ArticleId = id;
        if (id is null)
        {
            return Page();
        }

        using var response = await newsClient.GetAsync(id.Value, cancellationToken);
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
            ErrorMessage = localizer["News.Edit.LoadFailed"].Value;
            return Page();
        }

        var article = await response.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken);
        Input = NewsInput.From(article);
        ETag = response.Headers.ETag?.Tag;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        HttpResponseMessage response;
        if (ArticleId is null)
        {
            response = await newsClient.CreateAsync(Input, cancellationToken);
        }
        else
        {
            if (ETag is null)
            {
                ErrorMessage = localizer["Common.MissingETag"].Value;
                return Page();
            }

            response = await newsClient.UpdateAsync(ArticleId.Value, ETag, Input, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // 412：并发令牌过期。输入完整保留，提供“查看最新内容”出路而不是让用户刷新。
                ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
                ShowLatestLink = true;
                LatestUrl = $"/News/Detail?id={ArticleId}";
                return Page();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
                return Page();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ErrorMessage = localizer["Common.ValidationFailed"].Value;
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["News.Edit.SaveFailed"].Value;
                return Page();
            }
        }

        if (ArticleId is null)
        {
            TempData["StatusMessage"] = localizer["News.Edit.Created"].Value;
            return RedirectToPage("/News/Index");
        }

        // 编辑保存后进入详情页，保留当前记录上下文。
        TempData["StatusMessage"] = localizer["News.Edit.Updated"].Value;
        return RedirectToPage("/News/Detail", new { id = ArticleId.Value });
    }
}
