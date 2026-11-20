// 行政区详情页。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Models;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.GeoDivisions;

public sealed class DetailModel(
    GeoDivisionClient regionClient,
    ReferenceEnumClient enumClient,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GeoDivisionView? GeoDivision { get; private set; }

    // Kind 显示名（来自枚举字典 reference_enums）
    public IReadOnlyDictionary<string, string> KindNames { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Kind 显示名；字典缺失时回退 code，再回退深度标签（非省市区文案）。</summary>
    public string KindNameFor(string? kind, int level)
    {
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var code = kind.Trim().ToLowerInvariant();
            if (KindNames.TryGetValue(code, out var name))
            {
                return name;
            }

            return code;
        }

        return level is >= 1 and <= 4 ? localizer[$"GeoDivisions.Level.{level}"].Value : $"L{level}";
    }

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public string? CountryCode { get; set; }

    /// <summary>详情页渲染时取得的并发令牌，删除时回传为 If-Match。</summary>
    [BindProperty]
    public string? ETag { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        KindNames = await enumClient.LoadGeoDivisionKindNamesAsync(cancellationToken);
        var failure = await LoadRegionAsync(id, cancellationToken);
        return failure ?? Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ETag))
        {
            ErrorMessage = localizer["Common.MissingETag"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        using var response = await regionClient.DeleteAsync(id, ETag, cancellationToken);
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
            ErrorMessage = localizer["GeoDivisions.Detail.DeleteHasChildren"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        if (response.StatusCode == HttpStatusCode.PreconditionFailed
            || response.StatusCode == HttpStatusCode.Conflict)
        {
            // 删除时的 412 只可能是并发令牌过期：记录已被他人改过。
            ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["GeoDivisions.Detail.DeleteFailed"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        StatusMessage = localizer["GeoDivisions.Detail.Deleted"].Value;
        return RedirectToPage("/GeoDivisions/Index", new { countryCode = CountryCode });
    }

    private async Task<IActionResult> ReloadAfterDeleteFailureAsync(Guid id, CancellationToken cancellationToken)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        return failure ?? Page();
    }

    private async Task<IActionResult?> LoadRegionAsync(Guid id, CancellationToken cancellationToken)
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
            ErrorMessage = localizer["GeoDivisions.Detail.LoadFailed"].Value;
            return null;
        }

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        ETag = response.Headers.ETag?.Tag;
        return null;
    }
}
