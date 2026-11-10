// 新增或编辑新闻，编辑时携带 ETag 完成乐观并发更新，并维护非默认语言的译文。
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

public sealed class EditModel(
    NewsClient newsClient,
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public NewsInput Input { get; set; } = new();

    [BindProperty]
    public Guid? ArticleId { get; set; }

    [BindProperty]
    public string? ETag { get; set; }

    [BindProperty]
    public Dictionary<string, NewsTranslationInput> Translations { get; set; } = [];

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    public bool IsEdit => ArticleId is not null;

    public string? ErrorMessage { get; private set; }

    public IReadOnlyList<string> EditableCultures => localizationOptions.SupportedCultures
        .Where(culture => !string.Equals(culture, localizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    /// <summary>译文编辑器数据：以当前表单中的默认语言内容作为原文参考。</summary>
    public TranslationEditorModel TranslationEditor => new(
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
                    Input.Title,
                    MaxLength: 200),
                new TranslationField(
                    localizer["Field.News.Summary"].Value,
                    $"Translations[{culture}].Summary",
                    translation?.Summary,
                    Input.Summary,
                    MaxLength: 500),
                new TranslationField(
                    localizer["Field.News.Content"].Value,
                    $"Translations[{culture}].Content",
                    translation?.Content,
                    Input.Content,
                    Multiline: true,
                    MaxLength: 50000));
        }).ToArray());

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        ArticleId = id;
        if (id is null)
        {
            return Page();
        }

        using var response = await newsClient.GetAsync(id.Value, cancellationToken);
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
            ErrorMessage = localizer["News.Edit.LoadFailed"].Value;
            return Page();
        }

        var article = await response.Content.ReadFromJsonAsync<NewsArticleView>(cancellationToken);
        Input = NewsInput.From(article);
        ETag = response.Headers.ETag?.Tag;

        var translationFailure = await LoadTranslationsAsync(id.Value, cancellationToken);
        return translationFailure ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        HttpResponseMessage response;
        if (ArticleId is null)
        {
            response = await newsClient.CreateAsync(Input, cancellationToken);
        }
        else
        {
            if (ETag is null)
            {
                ErrorMessage = localizer["Common.MissingETag"].Value;
                return Page();
            }

            response = await newsClient.UpdateAsync(ArticleId.Value, ETag, Input, cancellationToken);
        }

        string? updatedETag;
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ErrorMessage = localizer["Common.ConcurrencyConflict"].Value;
                return Page();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ErrorMessage = localizer["Common.ValidationFailed"].Value;
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = localizer["News.Edit.SaveFailed"].Value;
                return Page();
            }

            updatedETag = response.Headers.ETag?.Tag;
        }

        if (ArticleId is not null && TranslationsLoaded)
        {
            var (saved, actionResult) = await SaveTranslationsAsync(ArticleId.Value, cancellationToken);
            if (!saved)
            {
                if (actionResult is not null)
                {
                    return actionResult;
                }

                ETag = updatedETag;
                ErrorMessage = localizer["Translations.SaveFailed"].Value;
                return Page();
            }
        }

        if (ArticleId is null)
        {
            TempData["StatusMessage"] = localizer["News.Edit.Created"].Value;
            return RedirectToPage("/News/Index");
        }

        // 编辑保存后进入详情页，保留当前记录上下文。
        TempData["StatusMessage"] = localizer["News.Edit.Updated"].Value;
        return RedirectToPage("/News/Detail", new { id = ArticleId.Value });
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
