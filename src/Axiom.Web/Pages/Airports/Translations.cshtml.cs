// 维护机场在非默认语言下的名称与城市译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.Airports;

public sealed class TranslationsModel(
    ReferenceDataClient referenceDataClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer)
    : TranslationPageModel<AirportTranslationInput>(localizationOptions, localizer)
{
    public AirportResponse? Airport { get; private set; }

    /// <summary>默认语言原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public AirportResponse? DefaultLanguageAirport { get; private set; }

    protected override string RedirectPath => "/Airports/Translations";

    public TranslationEditorModel? TranslationEditor
    {
        get
        {
            var airport = Airport;
            if (airport is null)
            {
                return null;
            }

            var source = DefaultLanguageAirport ?? airport;
            return new TranslationEditorModel(
                TranslationEditorBuilder.CultureLabel(LocalizationOptions.DefaultCulture, Localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        Localizer,
                        new TranslationField(
                            Localizer["Field.Airport.Name"].Value,
                            $"Translations[{culture}].Name",
                            translation?.Name,
                            source.Name,
                            MaxLength: 200),
                        new TranslationField(
                            Localizer["Field.Airport.City"].Value,
                            $"Translations[{culture}].City",
                            translation?.City,
                            source.City,
                            MaxLength: 100));
                }).ToArray());
        }
    }

    protected override async Task<IActionResult?> LoadEntityAsync(Guid id, CancellationToken ct)
    {
        using var response = await referenceDataClient.GetAsync(id, ct);
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

        Airport = await response.Content.ReadFromJsonAsync<AirportResponse>(ct);

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await referenceDataClient.GetAsync(
            id, LocalizationOptions.DefaultCulture, ct);
        DefaultLanguageAirport = defaultResponse.IsSuccessStatusCode
            ? await defaultResponse.Content.ReadFromJsonAsync<AirportResponse>(ct)
            : null;

        return null;
    }

    protected override Task<HttpResponseMessage> GetTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        referenceDataClient.GetTranslationsAsync(id, culture, ct);

    protected override Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id, string culture, IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        referenceDataClient.SaveTranslationsAsync(id, culture, values, ct);

    protected override Task<HttpResponseMessage> DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        referenceDataClient.DeleteTranslationsAsync(id, culture, ct);

    protected override AirportTranslationInput MapFromView(TranslationView view) =>
        AirportTranslationInput.From(view);

    protected override AirportTranslationInput CreateEmptyInput() => new();

    protected override IReadOnlyDictionary<string, string> ExtractValues(AirportTranslationInput input) =>
        input.ToValues();
}
