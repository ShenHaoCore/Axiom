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
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public NewsArticleView? Article { get; private set; }

    /// <summary>默认语言原始数据：译文编辑器的原文参考，不随界面语言本地化。</summary>
    public NewsArticleView? DefaultLanguageArticle { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<string> EditableCultures => localizationOptions.SupportedCultures
        .Where(culture => !string.Equals(culture, localizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    /// <summary>译文编辑器数据：以新闻默认语言内容作为原文参考。</summary>
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
                TranslationEditorBuilder.CultureLabel(localizationOptions.DefaultCulture, localizer),
                EditableCultures.Select(culture =>
                {
                    Translations.TryGetValue(culture, out var translation);
                    return TranslationEditorBuilder.CreateSection(
                        culture,
                        localizer,
                        new TranslationField(
                            localizer["Field.News.Title"].Value,
                            $"Translations[{culture}].Title",
                            translation?.Title,
                            source.Title,
                            MaxLength: 200),
                        new TranslationField(
                            localizer["Field.News.Summary"].Value,
                            $"Translations[{culture}].Summary",
                            translation?.Summary,
                            source.Summary,
                            MaxLength: 500),
                        new TranslationField(
                            localizer["Field.News.Content"].Value,
                            $"Translations[{culture}].Content",
                            translation?.Content,
                            source.Content,
                            Multiline: true,
                            MaxLength: 50000));
                }).ToArray());
        }
    }

    [BindProperty]
    public Dictionary<string, NewsTranslationInput> Translations { get; set; } = [];

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var failure = await LoadArticleAsync(id, cancellationToken);
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
            return RedirectToPage("/News/Translations", new { id });
        }

        var failure = await LoadArticleAsync(id, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (Article is null)
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
        return RedirectToPage("/News/Translations", new { id });
    }

    private async Task<IActionResult?> LoadArticleAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await newsClient.GetAsync(id, cancellationToken);
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

        Article = await response.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken);
        if (Article is null)
        {
            return NotFound();
        }

        // 默认语言版本：显式以 DefaultCulture 再取一次，供译文原文参考。
        using var defaultResponse = await newsClient.GetAsync(
            id, localizationOptions.DefaultCulture, cancellationToken);
        DefaultLanguageArticle = defaultResponse.IsSuccessStatusCode
            ? await defaultResponse.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken)
            : null;

        return null;
    }

    private async Task<IActionResult?> LoadTranslationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<string, NewsTranslationInput>();
        foreach (var culture in EditableCultures)
        {
            using var response = await newsClient.GetTranslationsAsync(id, culture, cancellationToken);
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
                loaded[culture] = new NewsTranslationInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["Translations.LoadFailed"].Value;
                return null;
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationView>(cancellationToken);
            loaded[culture] = NewsTranslationInput.From(translation);
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
                ? await newsClient.DeleteTranslationsAsync(id, culture, cancellationToken)
                : await newsClient.SaveTranslationsAsync(id, culture, values, cancellationToken);

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
