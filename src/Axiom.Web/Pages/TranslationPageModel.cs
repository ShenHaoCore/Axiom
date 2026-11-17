// 翻译页面模板基类：统一 OnGet/OnPost 流程、状态码处理、翻译批量加载与保存。
// 子类只需提供实体加载、网关调用委托与 TranslationInput 的映射方法。
using System.Net;
using System.Net.Http.Json;
using Axiom.SharedKernel.Localization;
using Axiom.Web.Models;
using Axiom.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Axiom.Web.Pages;

/// <summary>翻译页面基类，封装翻译子资源的标准 CRUD + 翻译维护流程。</summary>
/// <typeparam name="TTranslationInput">页面表单中的翻译输入模型（AirportTranslationInput 等）。</typeparam>
public abstract class TranslationPageModel<TTranslationInput>(
    AxiomLocalizationOptions localizationOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    protected AxiomLocalizationOptions LocalizationOptions { get; } = localizationOptions;
    protected IStringLocalizer<SharedResource> Localizer { get; } = localizer;

    // ===== 子类必须提供 =====

    /// <summary>翻译保存成功后停留的重定向路径，如 "/Airports/Translations"。</summary>
    protected abstract string RedirectPath { get; }

    /// <summary>网关：获取指定实体的某语言译文。</summary>
    protected abstract Task<HttpResponseMessage> GetTranslationsAsync(
        Guid id, string culture, CancellationToken ct);

    /// <summary>网关：保存某语言译文。</summary>
    protected abstract Task<HttpResponseMessage> SaveTranslationsAsync(
        Guid id, string culture, IReadOnlyDictionary<string, string> values, CancellationToken ct);

    /// <summary>网关：删除某语言译文。</summary>
    protected abstract Task<HttpResponseMessage> DeleteTranslationsAsync(
        Guid id, string culture, CancellationToken ct);

    /// <summary>网关译文 → 页面输入模型。</summary>
    protected abstract TTranslationInput MapFromView(TranslationView view);

    /// <summary>空译文输入（网关返回 404 时用）。</summary>
    protected abstract TTranslationInput CreateEmptyInput();

    /// <summary>页面输入 → 网关期望的字段键值对。</summary>
    protected abstract IReadOnlyDictionary<string, string> ExtractValues(TTranslationInput input);

    // ===== 基类统一管理的页面状态 =====

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; protected set; }

    [BindProperty]
    public bool TranslationsLoaded { get; set; }

    [BindProperty]
    public Dictionary<string, TTranslationInput> Translations { get; set; } = [];

    public IReadOnlyList<string> EditableCultures => LocalizationOptions.SupportedCultures
        .Where(c => !string.Equals(c, LocalizationOptions.DefaultCulture, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    // ===== 公共入口 =====

    /// <summary>
    /// 加载实体 + 所有语言译文 → 渲染翻译编辑器。
    /// 子类必须重写 LoadEntityAsync 以填充自己的 Entity 属性（并处理 401/403/404）。
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct = default)
    {
        var failure = await LoadEntityAsync(id, ct);
        if (failure is not null)
        {
            return failure;
        }

        var translationFailure = await LoadTranslationsAsync(id, ct);
        return translationFailure ?? Page();
    }

    /// <summary>
    /// 检查 TranslationsLoaded → 重新加载实体 → 批量保存各语言译文 → 停留本页。
    /// 子类必须重写 LoadEntityAsync 以确保 Entity 在保存阶段也可用。
    /// </summary>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct = default)
    {
        if (!TranslationsLoaded)
        {
            return RedirectToPage(RedirectPath, new { id });
        }

        var failure = await LoadEntityAsync(id, ct);
        if (failure is not null)
        {
            return failure;
        }

        var (saved, actionResult) = await SaveTranslationsAsync(id, ct);
        if (!saved)
        {
            if (actionResult is not null)
            {
                return actionResult;
            }

            ErrorMessage = Localizer["Translations.SaveFailed"].Value;
            return Page();
        }

        StatusMessage = Localizer["Translations.Saved"].Value;
        return RedirectToPage(RedirectPath, new { id });
    }

    // ===== 子类必须实现 =====

    /// <summary>
    /// 加载实体（含默认语言版本），填充子类自己的 Entity / DefaultLanguageXxx 属性。
    /// 返回 failure IActionResult（Challenge / Forbidden / NotFound）或 null 表示成功。
    /// </summary>
    protected abstract Task<IActionResult?> LoadEntityAsync(Guid id, CancellationToken ct);

    // ===== 受保护的公共实现 =====

    protected async Task<IActionResult?> LoadTranslationsAsync(Guid id, CancellationToken ct)
    {
        var loaded = new Dictionary<string, TTranslationInput>();
        foreach (var culture in EditableCultures)
        {
            using var response = await GetTranslationsAsync(id, culture, ct);
            var authFailure = this.HandleAuthStatus(response.StatusCode);
            if (authFailure is not null)
            {
                return authFailure;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                loaded[culture] = CreateEmptyInput();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = Localizer["Translations.LoadFailed"].Value;
                return null;
            }

            var translation = await response.Content.ReadFromJsonAsync<TranslationView>(ct);
            loaded[culture] = translation is not null ? MapFromView(translation) : CreateEmptyInput();
        }

        Translations = loaded;
        TranslationsLoaded = true;
        return null;
    }

    protected async Task<(bool Saved, IActionResult? ActionResult)> SaveTranslationsAsync(
        Guid id, CancellationToken ct)
    {
        foreach (var culture in EditableCultures)
        {
            Translations.TryGetValue(culture, out var input);
            IReadOnlyDictionary<string, string> values = input is not null ? ExtractValues(input) : new Dictionary<string, string>();
            using var response = values.Count == 0
                ? await DeleteTranslationsAsync(id, culture, ct)
                : await SaveTranslationsAsync(id, culture, values, ct);

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
