// Web 端共享的令牌名与 HTTP 头字面量，供各网关客户端与认证模块复用，避免重复硬编码。
namespace Axiom.Web.Clients;

public static class ApiClientHeaders
{
    // 认证令牌在 Cookie 属性与 OIDC 响应中的固定键名。
    public const string AccessToken = "access_token";
    public const string BearerScheme = "Bearer";

    // 各客户端转发到网关的 HTTP 头名。
    public const string AcceptLanguage = "Accept-Language";
    public const string IfMatch = "If-Match";
}
