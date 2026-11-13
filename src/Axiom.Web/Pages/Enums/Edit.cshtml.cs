// 编辑枚举字典显示名与排序，并维护非默认语言 Name 译文；Category/Code 只读。
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

public sealed class EditModel(
    ReferenceEnumClient enumClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public ReferenceEnumInput Input { get; set; } = new();

    public ReferenceEnumView? Item { get; private set; }

    /// <summary>默认语言原始名称：译文编辑器原文参考，不随界面语言本地化。</summary>
    public string? DefaultLanguageName { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<string> EditableCultures => localizationOptions.SupportedCultures
        .Where(culture => !string.Equals(culture, localizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public TranslationEditorModel? TranslationEditor
    {
        get
        {
            if (Item is null)
            {
                return null;
            }

            var sourceName = DefaultLanguageName ?? Item.Name;
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
        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        Input = ReferenceEnumInput.From(Item!);
        // 表单编辑主表 Name：始终展示默认语言原文，避免英文界面下误改译文。
        if (!string.IsNullOrWhiteSpace(DefaultLanguageName))
        {
            Input.Name = DefaultLanguageName;
        }

        var translationFailure = await LoadTranslationsAsync(id, cancellationToken);
        return translationFailure ?? Page();
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
        _ = await LoadTranslationsAsync(id, cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var response = await enumClient.UpdateAsync(id, Input, cancellationToken);
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
            ErrorMessage = localizer["Enums.EditFailed"].Value;
            return Page();
        }

        StatusMessage = localizer["Enums.Updated"].Value;
        return RedirectToPage("/Enums/Index", new { category = Item.Category });
    }

    public async Task<IActionResult> OnPostTranslationsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TranslationsLoaded)
        {
            return RedirectToPage("/Enums/Edit", new { id });
        }

        var failure = await LoadAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Item is not null)
        {
            Input = ReferenceEnumInput.From(Item);
            if (!string.IsNullOrWhiteSpace(DefaultLanguageName))
            {
                Input.Name = DefaultLanguageName;
            }
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
        return RedirectToPage("/Enums/Edit", new { id });
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

        Item = await response.Content.ReadFromJsonAsync<ReferenceEnumView>(cancellationToken);
        if (Item is null)
        {
            return NotFound();
        }

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

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                loaded[culture] = new ReferenceEnumTranslationInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["Translations.LoadFailed"].Value;
                return Page();
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
