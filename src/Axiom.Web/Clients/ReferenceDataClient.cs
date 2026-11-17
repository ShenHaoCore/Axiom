// 使用当前 SSO 会话的访问令牌调用机场网关。
using System.Net.Http.Json;
using Axiom.Web.Models;

namespace Axiom.Web.Clients;

public sealed class ReferenceDataClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : AuthenticatedApiClient(httpClient, httpContextAccessor)
{
    internal const string AIRPORTS_PATH = "/api/reference-data/airports";

    protected override string EntityPath => AIRPORTS_PATH;

    public Task<HttpResponseMessage> ListAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, AIRPORTS_PATH, null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, AIRPORTS_PATH, JsonContent.Create(input), cancellationToken);

    public Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string eTag,
        AirportInput input,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{AIRPORTS_PATH}/{id}", JsonContent.Create(input), cancellationToken, eTag);

    public Task<HttpResponseMessage> DeleteAsync(
        Guid id,
        string eTag,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{AIRPORTS_PATH}/{id}", null, cancellationToken, eTag);
}
