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
    IStringLocalizer<SharedResource> localizer)
    : TranslationPageModel<ReferenceEnumTranslationInput>(localizationOptions, localizer)
{
    public ReferenceEnumView? Item { get; private set; }

    /// <summary>默认语言原始名称：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public string? DefaultLanguageName { get; private set; }

    protected override string RedirectPath => "/Enums/Translations";

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
                TranslationEditorBuilder.CultureLabel(LocalizationOptions.DefaultCulture, Localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        Localizer,
                        new TranslationField(
                            Localizer["Field.Enum.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            sourceName,
                            MaxLength: 100));
                }).ToArray());
        }
    }

    protected override async Task<IActionResult?> LoadEntityAsync(Guid id, CancellationToken ct)
    {
        using var response = await enumClient.GetAsync(id, ct);
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
            ErrorMessage = Localizer["Translations.LoadFailed"].Value;
            return null;
        }

        Item = await response.Content.ReadFromJsonAsync<ReferenceEnumView>(ct);
        if (Item is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await enumClient.GetAsync(
            id, LocalizationOptions.DefaultCulture, ct);
        DefaultLanguageName = defaultResponse.IsSuccessStatusCode
            ? (await defaultResponse.Content.ReadFromJsonAsync<ReferenceEnumView>(ct))?.Name
            : Item.Name;

        return null;
    }

    protected override Task<HttpResponseMessage> GetTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        enumClient.GetTranslationsAsync(id, culture, ct);

    protected override Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id, string culture, IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        enumClient.SaveTranslationsAsync(id, culture, values, ct);

    protected override Task<HttpResponseMessage> DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        enumClient.DeleteTranslationsAsync(id, culture, ct);

    protected override ReferenceEnumTranslationInput MapFromView(TranslationView view) =>
        ReferenceEnumTranslationInput.From(view);

    protected override ReferenceEnumTranslationInput CreateEmptyInput() => new();

    protected override IReadOnlyDictionary<string, string> ExtractValues(ReferenceEnumTranslationInput input) =>
        input.ToValues();
}
