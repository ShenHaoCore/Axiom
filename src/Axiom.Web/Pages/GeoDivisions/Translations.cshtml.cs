// 独立维护行政区在非默认语言下的名称译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.GeoDivisions;

public sealed class TranslationsModel(
    GeoDivisionClient regionClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GeoDivisionView? GeoDivision { get; private set; }

    /// <summary>默认语言原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public GeoDivisionView? DefaultLanguageDivision { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

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

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var translationFailure = await LoadTranslationsAsync(id, cancellationToken);
        return translationFailure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TranslationsLoaded)
        {
            return RedirectToPage("/GeoDivisions/Translations", new { id });
        }

        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (GeoDivision is null)
        {
            return Page();
        }

        var (saved, actionResult) = await SaveTranslationsAsync(id, cancellationToken);
        if (!saved)
        {
            if (actionResult is not null)
            {
                return actionResult;
            }

            ErrorMessage = localizer["Translations.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Translations.Saved"].Value;
        // 保存后留在翻译页，方便继续维护其他语言。
        return RedirectToPage("/GeoDivisions/Translations", new { id });
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
            ErrorMessage = localizer["Translations.LoadFailed"].Value;
            return null;
        }

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(cancellationToken);
        if (GeoDivision is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
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
            var authFailure = this.HandleAuthStatus(response.StatusCode);
            if (authFailure is not null)
            {
                return authFailure;
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

            var authFailure = this.HandleAuthStatus(response.StatusCode);
            if (authFailure is not null)
            {
                return (false, authFailure);
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                return (false, null);
            }
        }

        return (true, null);
    }
}
