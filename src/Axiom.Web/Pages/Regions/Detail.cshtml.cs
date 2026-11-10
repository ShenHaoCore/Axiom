// 行政区详情页，并维护非默认语言下的名称译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Regions;

public sealed class DetailModel(
    RegionClient regionClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public RegionView? Region { get; private set; }

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
            var region = Region;
            if (region is null)
            {
                return null;
            }

            return new TranslationEditorModel(
                TranslationEditorBuilder.CultureLabel(localizationOptions.DefaultCulture, localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        localizer,
                        new TranslationField(
                            localizer["Field.Region.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            region.Name,
                            MaxLength: 200));
                }).ToArray());
        }
    }

    [BindProperty]
    public Dictionary<string, RegionTranslationInput> Translations { get; set; } = [];

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    [BindProperty]
    public string? CountryCode { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Region is null)
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
            return RedirectToPage("/Regions/Detail", new { id });
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
        return RedirectToPage("/Regions/Detail", new { id });
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
            ErrorMessage = localizer["Regions.Detail.DeleteHasChildren"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = localizer["Regions.Detail.DeleteFailed"].Value;
            return await ReloadAfterDeleteFailureAsync(id, cancellationToken);
        }

        StatusMessage = localizer["Regions.Detail.Deleted"].Value;
        return RedirectToPage("/Regions/Index", new { countryCode = CountryCode });
    }

    private async Task<IActionResult> ReloadAfterDeleteFailureAsync(Guid id, CancellationToken cancellationToken)
    {
        var failure = await LoadRegionAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Region is null)
        {
            return Page();
        }

        var translationFailure = await LoadTranslationsAsync(id, cancellationToken);
        return translationFailure ?? Page();
    }

    private async Task<IActionResult?> LoadRegionAsync(Guid id, CancellationToken cancellationToken)
    {
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
            ErrorMessage = localizer["Regions.Detail.LoadFailed"].Value;
            return null;
        }

        Region = await response.Content.ReadFromJsonAsync<RegionView>(cancellationToken);
        return null;
    }

    private async Task<IActionResult?> LoadTranslationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<string, RegionTranslationInput>();
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
                loaded[culture] = new RegionTranslationInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["Translations.LoadFailed"].Value;
                return null;
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationView>(cancellationToken);
            loaded[culture] = RegionTranslationInput.From(translation);
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
