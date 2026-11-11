// 集中注册 Web 的 Cookie、OIDC 与开发环境 Mock 认证。
using Axiom.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Globalization;

namespace Axiom.Web.Authentication;

/// <summary>
/// 提供用于配置Web身份验证的扩展方法。
/// </summary>
internal static class WebAuthenticationExtensions
{
    /// <summary>
    /// 为应用程序添加了网页身份验证功能。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="useMockAuth"></param>
    public static void AddWebAuthentication(this WebApplicationBuilder builder, bool useMockAuth)
    {
        builder.Services.AddScoped<RefreshCookieEvents>();

        var authentication = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = useMockAuth ? MockAuthenticationHandler.SCHEME : CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = useMockAuth ? MockAuthenticationHandler.SCHEME : OpenIdConnectDefaults.AuthenticationScheme;
        }).AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.EventsType = typeof(RefreshCookieEvents);
        }).AddOpenIdConnect(options =>
        {
            var authority = (builder.Configuration["Oidc:Authority"] ?? "http://localhost:5101").TrimEnd('/');
            var issuer = builder.Configuration["Oidc:ValidIssuer"] ?? $"{authority}/";
            options.Authority = authority;
            options.ClientId = builder.Configuration["Oidc:ClientId"] ?? "axiom-web";
            options.CallbackPath = "/callback";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;
            // 登录页语言跟随 Web 当前界面语言，AuthHub 按 ui_locales 本地化。
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.UiLocales = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            };
            options.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata", !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"));
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.ValidIssuer = issuer;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("offline_access");
            options.Scope.Add(ReferenceDataPermissions.READ);
            options.Scope.Add(ReferenceDataPermissions.MANAGE);
            options.Scope.Add(NewsPermissions.READ);
            options.Scope.Add(NewsPermissions.MANAGE);

            var backchannelAuthority = builder.Configuration["Oidc:BackchannelAuthority"]?.TrimEnd('/');
            if (!string.IsNullOrEmpty(backchannelAuthority))
            {
                var metadataAddress = $"{backchannelAuthority}/.well-known/openid-configuration";
                options.MetadataAddress = metadataAddress;
                var documentRetriever = new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata };
                options.ConfigurationManager = new PublicAuthorizationEndpointConfigurationManager(
                    new ConfigurationManager<OpenIdConnectConfiguration>(
                        metadataAddress,
                        new OpenIdConnectConfigurationRetriever(),
                        documentRetriever),
                    authority);
            }
        });

        if (useMockAuth)
        {
            authentication.AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(MockAuthenticationHandler.SCHEME, _ => { });
        }

        builder.Services.AddAuthorization();
    }
}
