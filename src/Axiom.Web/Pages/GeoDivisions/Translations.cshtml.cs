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
    IStringLocalizer<SharedResource> localizer)
    : TranslationPageModel<GeoDivisionTranslationInput>(localizationOptions, localizer)
{
    public GeoDivisionView? GeoDivision { get; private set; }

    /// <summary>默认语言原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public GeoDivisionView? DefaultLanguageDivision { get; private set; }

    protected override string RedirectPath => "/GeoDivisions/Translations";

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
                TranslationEditorBuilder.CultureLabel(LocalizationOptions.DefaultCulture, Localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        Localizer,
                        new TranslationField(
                            Localizer["Field.GeoDivision.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            sourceName,
                            MaxLength: 200));
                }).ToArray());
        }
    }

    protected override async Task<IActionResult?> LoadEntityAsync(Guid id, CancellationToken ct)
    {
        using var response = await regionClient.GetAsync(id, ct);
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

        GeoDivision = await response.Content.ReadFromJsonAsync<GeoDivisionView>(ct);
        if (GeoDivision is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await regionClient.GetAsync(
            id, LocalizationOptions.DefaultCulture, ct);
        DefaultLanguageDivision = defaultResponse.IsSuccessStatusCode
            ? await defaultResponse.Content.ReadFromJsonAsync<GeoDivisionView>(ct)
            : null;

        return null;
    }

    protected override Task<HttpResponseMessage> GetTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        regionClient.GetTranslationsAsync(id, culture, ct);

    protected override Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id, string culture, IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        regionClient.SaveTranslationsAsync(id, culture, values, ct);

    protected override Task<HttpResponseMessage> DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        regionClient.DeleteTranslationsAsync(id, culture, ct);

    protected override GeoDivisionTranslationInput MapFromView(TranslationView view) =>
        GeoDivisionTranslationInput.From(view);

    protected override GeoDivisionTranslationInput CreateEmptyInput() => new();

    protected override IReadOnlyDictionary<string, string> ExtractValues(GeoDivisionTranslationInput input) =>
        input.ToValues();
}
