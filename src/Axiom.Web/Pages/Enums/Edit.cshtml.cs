// 编辑枚举字典显示名与排序；Category/Code 只读。写操作携带 If-Match 并发令牌。
using System.Net;
using System.Net.Http.Json;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Enums;

public sealed class EditModel(
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private string? _eTag;

    [BindProperty]
    public ReferenceEnumInput Input { get; set; } = new();

    public ReferenceEnumView? Item { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = ReferenceEnumInput.From(Item!);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Item is null)
        {
            return Page();
        }

        Input.Category = Item.Category;
        Input.Code = Item.Code;
        // Category/Code 只读展示、不随表单提交：绑定期 [Required] 会给这两个键记上
        // “字段必填”错误。它们由服务端从已加载实体补齐，先移除对应键再校验，
        // 只保留用户真正可编辑字段（Name/SortOrder）的校验错误。
        // 这是存量缺陷：此前保存从未真正生效，页面总在无声的 IsValid=false 上返回。
        // ModelState 键带页面模型前缀（Razor Pages 约定为 "Input.<属性>"）。
        ModelState.Remove("Input.Category");
        ModelState.Remove("Input.Code");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await enumClient.UpdateAsync(id, _eTag ?? string.Empty, Input, cancellationToken);
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

        if (response.StatusCode == HttpStatusCode.Conflict
            || response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Enums.EditFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Enums.Updated"].Value;
        return RedirectToPage("/Enums/Index", new { category = Item.Category });
    }

    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await enumClient.GetAsync(id, cancellationToken);
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
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            return Page();
        }

        _eTag = response.Headers.ETag?.Tag;
        Item = await response.Content.ReadFromJsonAsync<ReferenceEnumView>(cancellationToken);
        if (Item is null)
        {
            return NotFound();
        }

        return null;
    }
}
