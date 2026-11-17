// 维护新闻在非默认语言下的标题、摘要与正文译文。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Clients;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages.News;

public sealed class TranslationsModel(
    NewsClient newsClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer)
    : TranslationPageModel<NewsTranslationInput>(localizationOptions, localizer)
{
    public NewsArticleView? Article { get; private set; }

    /// <summary>默认语言原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public NewsArticleView? DefaultLanguageArticle { get; private set; }

    protected override string RedirectPath => "/News/Translations";

    public TranslationEditorModel? TranslationEditor
    {
        get
        {
            var article = Article;
            if (article is null)
            {
                return null;
            }

            var source = DefaultLanguageArticle ?? article;
            return new TranslationEditorModel(
                TranslationEditorBuilder.CultureLabel(LocalizationOptions.DefaultCulture, Localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        Localizer,
                        new TranslationField(
                            Localizer["Field.News.Title"].Value,
                            $"Translations[{culture}].Title",
                            translation?.Title,
                            source.Title,
                            MaxLength: 200),
                        new TranslationField(
                            Localizer["Field.News.Summary"].Value,
                            $"Translations[{culture}].Summary",
                            translation?.Summary,
                            source.Summary,
                            MaxLength: 500),
                        new TranslationField(
                            Localizer["Field.News.Content"].Value,
                            $"Translations[{culture}].Content",
                            translation?.Content,
                            source.Content,
                            Multiline: true,
                            MaxLength: 50000));
                }).ToArray());
        }
    }

    protected override async Task<IActionResult?> LoadEntityAsync(Guid id, CancellationToken ct)
    {
        using var response = await newsClient.GetAsync(id, ct);
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

        Article = await response.Content.ReadFromJsonAsync<NewsArticleView>(ct);
        if (Article is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await newsClient.GetAsync(
            id, LocalizationOptions.DefaultCulture, ct);
        DefaultLanguageArticle = defaultResponse.IsSuccessStatusCode
            ? await defaultResponse.Content.ReadFromJsonAsync<NewsArticleView>(ct)
            : null;

        return null;
    }

    protected override Task<HttpResponseMessage> GetTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        newsClient.GetTranslationsAsync(id, culture, ct);

    protected override Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id, string culture, IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        newsClient.SaveTranslationsAsync(id, culture, values, ct);

    protected override Task<HttpResponseMessage> DeleteTranslationsAsync(Guid id, string culture, CancellationToken ct) =>
        newsClient.DeleteTranslationsAsync(id, culture, ct);

    protected override NewsTranslationInput MapFromView(TranslationView view) =>
        NewsTranslationInput.From(view);

    protected override NewsTranslationInput CreateEmptyInput() => new();

    protected override IReadOnlyDictionary<string, string> ExtractValues(NewsTranslationInput input) =>
        input.ToValues();
}
