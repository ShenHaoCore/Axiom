// 新增或编辑新闻，编辑时携带 ETag 完成乐观并发更新。
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Axiom.Web.Pages.News;

public sealed class EditModel(NewsClient newsClient) : PageModel
{
    [BindProperty]
    public NewsInput Input { get; set; } = new();

    [BindProperty]
    public Guid? ArticleId { get; set; }

    [BindProperty]
    public string? ETag { get; set; }

    public bool IsEdit => ArticleId is not null;

    public string? ErrorMessage { get; private set; }

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
            ErrorMessage = "无法加载新闻内容，请稍后重试。";
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
                ErrorMessage = "缺少并发令牌，请刷新页面后重试。";
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

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ErrorMessage = "新闻已被其他请求修改，请刷新后重试。";
                return Page();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ErrorMessage = "提交内容未通过服务端校验，请检查后重试。";
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "保存新闻失败，请稍后重试。";
                return Page();
            }
        }

        TempData["StatusMessage"] = ArticleId is null ? "新闻已创建。" : "新闻已更新。";
        return RedirectToPage("/News/Index");
    }
}
