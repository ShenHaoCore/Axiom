// 将内网发现文档中的授权端点改写为浏览器可访问的公网地址，令牌端点保持内网地址。
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Axiom.Web;

public sealed class PublicAuthorizationEndpointConfigurationManager(
    IConfigurationManager<OpenIdConnectConfiguration> inner,
    string publicAuthority) : IConfigurationManager<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = await inner.GetConfigurationAsync(cancellationToken);
        configuration.AuthorizationEndpoint = $"{publicAuthority.TrimEnd('/')}/connect/authorize";
        return configuration;
    }

    public void RequestRefresh() => inner.RequestRefresh();
}
