// 行政区详情页，并维护非默认语言下的名称译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
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
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GeoDivisionView? GeoDivision { get; private set; }

    // Kind 显示名（来自枚举字典 reference_enums）
    public IReadOnlyDictionary<string, string> KindNames { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Kind 显示名；字典缺失或未知类型回退到深度通用文案。</summary>
    public string KindNameFor(string? kind, int level)
    {
        if (!string.IsNullOrWhiteSpace(kind)
            && KindNames.TryGetValue(kind.Trim().ToLowerInvariant(), out var name))
        {
            return name;
        }

        return level is >= 1 and <= 4 ? localizer[$"GeoDivisions.Level.{level}"].Value : $"L{level}";
    }

    /// <summary>默认语言（简体中文）原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public GeoDivisionView? DefaultLanguageDivision { get; private set; }

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyList<string> EditableCultures => localizationOptions.SupportedCultures
        .Where(culture => !string.Equals(culture, localizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    /// <summary>译文编辑器数据：以行政区默认语言名称作为原文参考。</summary>
    public TranslationEditorModel? TranslationEditor
    {
        get
        {
            var region = GeoDivision;
            if (region is null)
            {
                return null;
            }

            // 原文参考取默认语言原始名称，避免界面语言切换后原文页签跟着变。
            var sourceName = DefaultLanguageDivision?.Name ?? region.Name;

            return new TranslationEditorModel(
                TranslationEditorBuilder.CultureLabel(localizationOptions.DefaultCulture, localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        localizer,
                        new TranslationField(
                            localizer["Field.GeoDivision.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            sourceName,
                            MaxLength: 200));
                }).ToArray());
        }
    }

    [BindProperty]
    public Dictionary<string, GeoDivisionTranslationInput> Translations { get; set; } = [];

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    [BindProperty]
    public string? CountryCode { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await LoadKindNamesAsync(cancellationToken);
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (GeoDivision is null)
        {
            return Page();
        }

        var translationFailure = await LoadTranslationsAsync(id, cancellationToken);
        return translationFailure ?? Page();
    }

    public async Task<IActionResult> OnPostTranslationsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TranslationsLoaded)
        {
            return RedirectToPage("/GeoDivisions/Detail", new { id });
        }

        var (saved, actionResult) = await SaveTranslationsAsync(id, cancellationToken);
        if (!saved)
        {
            if (actionResult is not null)
            {
                return actionResult;
            }

            ErrorMessage = localizer["Translations.SaveFailed"].Value;
            var failure = await LoadRegionAsync(id, cancellationToken);
            return failure ?? Page();
        }

        TempData["StatusMessage"] = localizer["Translations.Saved"].Value;
        return RedirectToPage("/GeoDivisions/Detail", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await regionClient.DeleteAsync(id, cancellationToken);
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

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = localizer["GeoDivisions.Detail.DeleteHasChildren"].Value;
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
        if (failure is not null)
        {
            return failure;
        }

        if (GeoDivision is null)
        {
            return Page();
        }

        var translationFailure = await LoadTranslationsAsync(id, cancellationToken);
        return translationFailure ?? Page();
    }

    private async Task LoadKindNamesAsync(CancellationToken cancellationToken)
    {
        using var response = await enumClient.ListAsync(
            ReferenceEnumClient.GeoDivisionKindCategory,
            pageSize: 200,
            cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumView>>(cancellationToken);
        if (page is null)
        {
            return;
        }

        KindNames = page.Items
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
    }

    private async Task<IActionResult?> LoadRegionAsync(Guid id, CancellationToken cancellationToken)
    {
        // 界面语言本地化版本：用于标题与详情字段展示。
        using var response = await regionClient.GetAsync(id, cancellationToken);
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
            ErrorMessage = localizer["GeoDivisions.Detail.LoadFailed"].Value;
            return null;
        }

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);

        // 默认语言版本：显式以 DefaultCulture 作为 Accept-Language 再取一次，供原文页签使用。
        using var defaultResponse = await regionClient.GetAsync(
            id, localizationOptions.DefaultCulture, cancellationToken);
        DefaultLanguageDivision = defaultResponse.IsSuccessStatusCode
            ? await defaultResponse.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken)
            : null;

        return null;
    }

    private async Task<IActionResult?> LoadTranslationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<string, GeoDivisionTranslationInput>();
        foreach (var culture in EditableCultures)
        {
            using var response = await regionClient.GetTranslationsAsync(id, culture, cancellationToken);
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
                loaded[culture] = new GeoDivisionTranslationInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["Translations.LoadFailed"].Value;
                return null;
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationView>(cancellationToken);
            loaded[culture] = GeoDivisionTranslationInput.From(translation);
        }

        Translations = loaded;
        TranslationsLoaded = true;
        return null;
    }

    private async Task<(bool Saved, IActionResult? ActionResult)> SaveTranslationsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        foreach (var culture in EditableCultures)
        {
            Translations.TryGetValue(culture, out var input);
            var values = input?.ToValues() ?? new Dictionary<string, string>();
            using var response = values.Count == 0
                ? await regionClient.DeleteTranslationsAsync(id, culture, cancellationToken)
                : await regionClient.SaveTranslationsAsync(id, culture, values, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (false, Challenge());
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, StatusCode(StatusCodes.Status403Forbidden));
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                return (false, null);
            }
        }

        return (true, null);
    }
}
