// 关键词搜索辅助：转义 LIKE/ILIKE 通配符，避免用户输入被解释为模式。
namespace Axiom.SharedKernel.Models;

public static class LikePattern
{
    /// <summary>转义 % _ \ 三个通配符，配合 EF.Functions.ILike 的默认转义符（反斜杠）使用。</summary>
    public static string Escape(string keyword) =>
        keyword.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
