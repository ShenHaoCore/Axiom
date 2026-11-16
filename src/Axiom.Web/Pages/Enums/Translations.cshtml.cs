// 独立维护枚举字典项在非默认语言下的名称译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Enums;

public sealed class TranslationsModel(
    ReferenceEnumClient enumClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public ReferenceEnumView? Item { get; private set; }

    /// <summary>默认语言原始名称：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public string? DefaultLanguageName { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<string> EditableCultures => localizationOptions.SupportedCultures
        .Where(culture => !string.Equals(culture, localizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    /// <summary>译文编辑器数据：以枚举项默认语言名称作为原文参考。</summary>
    public TranslationEditorModel? TranslationEditor
    {
        get
        {
            var item = Item;
            if (item is null)
            {
                return null;
            }

            var sourceName = DefaultLanguageName ?? item.Name;
            return new TranslationEditorModel(
                TranslationEditorBuilder.CultureLabel(localizationOptions.DefaultCulture, localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        localizer,
                        new TranslationField(
                            localizer["Field.Enum.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            sourceName,
                            MaxLength: 100));
                }).ToArray());
        }
    }

    [BindProperty]
    public Dictionary<string, ReferenceEnumTranslationInput> Translations { get; set; } = [];

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadItemAsync(id, cancellationToken);
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
            return RedirectToPage("/Enums/Translations", new { id });
        }

        var failure = await LoadItemAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Item is null)
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
        return RedirectToPage("/Enums/Translations", new { id });
    }

    private async Task<IActionResult?> LoadItemAsync(Guid id, CancellationToken cancellationToken)
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
            ErrorMessage = localizer["Translations.LoadFailed"].Value;
            return null;
        }

        Item = await response.Content.ReadFromJsonAsync<ReferenceEnumView>(cancellationToken);
        if (Item is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await enumClient.GetAsync(
            id, localizationOptions.DefaultCulture, cancellationToken);
        DefaultLanguageName = defaultResponse.IsSuccessStatusCode
            ? (await defaultResponse.Content.ReadFromJsonAsync<ReferenceEnumView>(cancellationToken))?.Name
            : Item.Name;

        return null;
    }

    private async Task<IActionResult?> LoadTranslationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<string, ReferenceEnumTranslationInput>();
        foreach (var culture in EditableCultures)
        {
            using var response = await enumClient.GetTranslationsAsync(id, culture, cancellationToken);
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
                loaded[culture] = new ReferenceEnumTranslationInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["Translations.LoadFailed"].Value;
                return null;
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationView>(cancellationToken);
            loaded[culture] = ReferenceEnumTranslationInput.From(translation);
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
            var values = input?.ToValues() ?? [];
            using var response = values.Count == 0
                ? await enumClient.DeleteTranslationsAsync(id, culture, cancellationToken)
                : await enumClient.SaveTranslationsAsync(id, culture, values, cancellationToken);

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
