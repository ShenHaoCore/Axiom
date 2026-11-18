// 编辑行政区名称、代码、地理信息和父级；层级与国家等标识字段保持只读。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Errors;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.GeoDivisions;

public sealed class EditModel(
    GeoDivisionClient regionClient,
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public GeoDivisionInput Input { get; set; } = new();

    /// <summary>GET 时从响应头取得的并发令牌，提交时原样回传为 If-Match。</summary>
    [BindProperty]
    public string? ETag { get; set; }

    public GeoDivisionView? GeoDivision { get; private set; }

    public string SelectedParentLabel { get; private set; } = string.Empty;

    /// <summary>当前类型的显示名；选项缺失时回退原始编码（国家行只读展示用）。</summary>
    public string SelectedKindLabel { get; private set; } = string.Empty;

    public IReadOnlyList<SelectListItem> KindOptions { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = GeoDivisionInput.From(GeoDivision);
        await LoadKindOptionsAsync(cancellationToken);
        await LoadSelectedParentLabelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 保留用户提交值与提交的 ETag，只加载展示所需数据。
        var failure = await LoadRegionAsync(id, cancellationToken, resetInput: false);
        if (failure is not null)
        {
            return failure;
        }

        if (GeoDivision is null)
        {
            return Page();
        }

        // 标识字段以服务端实体为准，防止隐藏字段被篡改。
        Input.Level = GeoDivision.Level;
        Input.CountryCode = GeoDivision.CountryCode;
        Input.SubdivisionCode = GeoDivision.SubdivisionCode;
        if (Input.Level == 1)
        {
            Input.ParentId = null;
            // 国家行不再渲染类型表单项（只读展示），类型以实体为准。
            Input.Kind = GeoDivision.Kind;
        }

        await LoadKindOptionsAsync(cancellationToken);
        await LoadSelectedParentLabelAsync(cancellationToken);
        await ValidateParentAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await regionClient.UpdateAsync(id, Input, ETag, cancellationToken);
        var authFailure = this.HandleAuthStatus(response.StatusCode);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = localizer["Common.ValidationFailed"].Value;
            return Page();
        }

        if (response.StatusCode == HttpStatusCode.PreconditionRequired)
        {
            ErrorMessage = localizer["Common.MissingETag"].Value;
            return Page();
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // 409 有两个来源：并发令牌过期与唯一键冲突，靠问题详情里的 errorCode 区分。
            ErrorMessage = await IsConcurrencyConflictAsync(response, cancellationToken)
                ? localizer["Common.ConcurrencyConflict"].Value
                : localizer["GeoDivisions.Form.UniqueConflict"].Value;
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.Edit.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["GeoDivisions.Edit.Updated"].Value;
        return RedirectToPage("/GeoDivisions/Detail", new { id });
    }

    private async Task<IActionResult?> LoadRegionAsync(
        Guid id,
        CancellationToken cancellationToken,
        bool resetInput = true)
    {
        using var response = await regionClient.GetAsync(id, cancellationToken);
        var authFailure = this.HandleAuthStatus(response.StatusCode);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.Edit.LoadFailed"].Value;
            return null;
        }

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (GeoDivision is null)
        {
            ErrorMessage = localizer["GeoDivisions.Edit.LoadFailed"].Value;
            return null;
        }

        if (resetInput)
        {
            ETag = response.Headers.ETag?.Tag;
        }

        return null;
    }

    /// <summary>并发冲突与唯一键冲突同为 409，用问题详情的 errorCode 扩展区分。</summary>
    private static async Task<bool> IsConcurrencyConflictAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        return problem?.Extensions.TryGetValue("errorCode", out var code) == true
            && string.Equals(
                code?.ToString(),
                ApiErrorCodes.GEO_DIVISION_CONCURRENCY_CONFLICT,
                StringComparison.Ordinal);
    }

    private async Task<IActionResult?> LoadKindOptionsAsync(CancellationToken cancellationToken)
    {
        // 与新建页一致：按国家分域字典；该国家未维护时回退通用域。
        // 国家行（L=1）固定读通用域：country 仅在通用域维护。
        var level = GeoDivision?.Level ?? Input.Level;
        var countryCode = level == 1 ? null : Input.CountryCode ?? GeoDivision?.CountryCode;
        var (items, status) = await enumClient.ListGeoDivisionKindsAsync(countryCode, cancellationToken);
        var authFailure = this.HandleAuthStatus(status);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (status != HttpStatusCode.OK)
        {
            ErrorMessage = localizer["Enums.LoadFailed"].Value;
            KindOptions = [];
            return null;
        }

        KindOptions = GeoDivisionKindOptions.ToSelectList(items, level);

        if (!string.IsNullOrWhiteSpace(Input.Kind)
            && KindOptions.All(option => !string.Equals(option.Value, Input.Kind, StringComparison.OrdinalIgnoreCase)))
        {
            KindOptions = KindOptions
                .Append(new SelectListItem(Input.Kind, Input.Kind))
                .ToList();
        }

        // 国家行只读展示当前类型名。
        SelectedKindLabel = KindOptions
            .FirstOrDefault(option => string.Equals(option.Value, Input.Kind, StringComparison.OrdinalIgnoreCase))?.Text
            ?? Input.Kind ?? string.Empty;

        return null;
    }

    private async Task LoadSelectedParentLabelAsync(CancellationToken cancellationToken)
    {
        SelectedParentLabel = string.Empty;
        if (Input.ParentId is not Guid parentId)
        {
            return;
        }

        using var response = await regionClient.GetAsync(parentId, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var parent = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (parent is not null)
        {
            SelectedParentLabel = $"{parent.Name} ({parent.Code})";
        }
    }

    private async Task ValidateParentAsync(CancellationToken cancellationToken)
    {
        if (Input.Level == 1 || GeoDivision is null)
        {
            return;
        }

        if (!Input.ParentId.HasValue)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentRequired"].Value);
            return;
        }

        if (Input.ParentId == GeoDivision.Id)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
            return;
        }

        using var response = await regionClient.GetAsync(Input.ParentId.Value, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
            return;
        }

        var parent = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (parent is null
            || parent.Level != GeoDivision.Level - 1
            || !string.Equals(parent.CountryCode, GeoDivision.CountryCode, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Input.ParentId", localizer["Validation.GeoDivision.ParentInvalid"].Value);
        }
    }
}
