// 在 Cookie 校验阶段使用 refresh_token 续期访问令牌，避免 15 分钟到期后整页重新登录。
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Axiom.Web.Authentication;

public sealed class RefreshCookieEvents(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<RefreshCookieEvents> logger) : CookieAuthenticationEvents
{
    private const string ACCESS_TOKEN = "access_token";
    private const string REFRESH_TOKEN = "refresh_token";
    private const string EXPIRES_AT = "expires_at";
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromMinutes(15);

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var tokens = context.Properties.GetTokens().ToList();
        var accessToken = tokens.FirstOrDefault(token => token.Name == ACCESS_TOKEN)?.Value;
        var refreshToken = tokens.FirstOrDefault(token => token.Name == REFRESH_TOKEN)?.Value;
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var expiresAt = ResolveExpiry(tokens, accessToken);
        if (expiresAt is not null && expiresAt - DateTimeOffset.UtcNow > RefreshThreshold)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ResolveTokenEndpoint())
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = configuration["Oidc:ClientId"] ?? "axiom-web"
                })
            };
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("刷新访问令牌失败，AuthHub 返回 {StatusCode}。", (int)response.StatusCode);
                await RejectAsync(context);
                return;
            }

            await using var content = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
            using var document = await JsonDocument.ParseAsync(
                content,
                cancellationToken: context.HttpContext.RequestAborted);
            var root = document.RootElement;
            if (!root.TryGetProperty(ACCESS_TOKEN, out var accessElement)
                || accessElement.GetString() is not { Length: > 0 } newAccessToken)
            {
                logger.LogWarning("刷新令牌响应缺少 access_token。");
                await RejectAsync(context);
                return;
            }

            var newRefreshToken = root.TryGetProperty(REFRESH_TOKEN, out var refreshElement)
                ? refreshElement.GetString() ?? refreshToken
                : refreshToken;
            var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
                && expiresElement.TryGetInt32(out var seconds)
                    ? seconds
                    : (int)FallbackLifetime.TotalSeconds;
            var tokenType = root.TryGetProperty("token_type", out var typeElement)
                ? typeElement.GetString() ?? "Bearer"
                : "Bearer";

            // 保留 id_token 等未变化的条目，仅替换访问令牌相关项。
            var updatedTokens = tokens
                .Where(token => token.Name is not (ACCESS_TOKEN or REFRESH_TOKEN or EXPIRES_AT or "token_type"))
                .Append(new AuthenticationToken { Name = ACCESS_TOKEN, Value = newAccessToken })
                .Append(new AuthenticationToken { Name = REFRESH_TOKEN, Value = newRefreshToken })
                .Append(new AuthenticationToken { Name = "token_type", Value = tokenType })
                .Append(new AuthenticationToken
                {
                    Name = EXPIRES_AT,
                    Value = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
                        .ToString("o", CultureInfo.InvariantCulture)
                })
                .ToList();

            context.Properties.StoreTokens(updatedTokens);
            context.ShouldRenew = true;
            logger.LogInformation("访问令牌已通过 refresh_token 续期。");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            logger.LogError(exception, "刷新访问令牌异常。");
            await RejectAsync(context);
        }
    }

    private DateTimeOffset? ResolveExpiry(List<AuthenticationToken> tokens, string accessToken)
    {
        var expiresAtValue = tokens.FirstOrDefault(token => token.Name == EXPIRES_AT)?.Value;
        if (DateTimeOffset.TryParse(
                expiresAtValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt))
        {
            return expiresAt;
        }

        return ReadJwtExpiry(accessToken);
    }

    private string ResolveTokenEndpoint()
    {
        var authority = configuration["Oidc:BackchannelAuthority"]
            ?? configuration["Oidc:Authority"]
            ?? "http://localhost:5101";
        return $"{authority.TrimEnd('/')}/connect/token";
    }

    private static DateTimeOffset? ReadJwtExpiry(string accessToken)
    {
        var segments = accessToken.Split('.');
        if (segments.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }
        catch (FormatException)
        {
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
