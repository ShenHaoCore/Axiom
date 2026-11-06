// 国际化配置：默认语言 + 受支持语言列表，由各宿主应用从 Localization 配置节绑定。
namespace Axiom.SharedKernel.Localization;

public sealed class AxiomLocalizationOptions
{
    public const string SECTION = "Localization";

    public string DefaultCulture { get; set; } = "zh-CN";

    public string[] SupportedCultures { get; set; } = ["zh-CN", "en"];

    /// <summary>翻译查询的候选语言链（解析结果 → 父语言 → 默认语言）。</summary>
    public IReadOnlyList<string> CultureChain(string? culture) =>
        AxiomCultureResolver.PendingChain(culture, SupportedCultures, DefaultCulture);

    /// <summary>写入翻译前校验语言是否受支持。</summary>
    public bool IsSupported(string? culture) =>
        AxiomCultureResolver.IsSupported(culture, SupportedCultures);
}
