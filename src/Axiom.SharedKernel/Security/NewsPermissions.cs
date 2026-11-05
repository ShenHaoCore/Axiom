// 集中定义新闻服务使用的读取和管理权限。
namespace Axiom.SharedKernel.Security;

public static class NewsPermissions
{
    public const string READ = "news.read";
    public const string MANAGE = "news.manage";
}
