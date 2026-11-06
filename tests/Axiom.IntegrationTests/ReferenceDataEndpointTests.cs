// 验证基础数据 API 的鉴权行为和完整 CRUD 链路。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Models;
using Axiom.SharedKernel.Models;
using Axiom.SharedKernel.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Axiom.IntegrationTests;

public sealed class ReferenceDataEndpointTests(ReferenceDataApiFactory factory)
    : IClassFixture<ReferenceDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/reference-data/airports");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("airports")]
    [InlineData("seaports")]
    [InlineData("rail-stations")]
    public async Task Get_with_read_permission_returns_200(string resource)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/reference-data/{resource}", ReferenceDataPermissions.READ);

        var response = await _client.SendAsync(request);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationResponse>>();
        result.Should().NotBeNull();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Post_with_only_read_permission_returns_403()
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/airports",
            ReferenceDataPermissions.READ,
            ValidAirport());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manage_permission_supports_complete_airport_crud()
    {
        using var createRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/airports",
            ManagePermissions,
            ValidAirport());
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ReferenceLocationResponse>();
        created.Should().NotBeNull();
        created!.Code.Should().Be("SHA");
        created.CountryCode.Should().Be("CN");
        createResponse.Headers.ETag.Should().NotBeNull();
        var createdETag = createResponse.Headers.ETag!.ToString();
        var location = createResponse.Headers.Location!.OriginalString;

        var getResponse = await _client.SendAsync(CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResponse.Headers.ETag?.ToString().Should().Be(createdETag);

        using var updateRequest = CreateJsonRequest(
            HttpMethod.Put,
            location,
            ManagePermissions,
            ValidAirport() with { Name = "上海浦东国际机场（更新）" },
            createdETag);
        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReferenceLocationResponse>();
        updated!.Name.Should().Contain("更新");
        var updatedETag = updateResponse.Headers.ETag?.ToString();
        updatedETag.Should().NotBeNullOrWhiteSpace();
        updatedETag.Should().NotBe(createdETag);

        var deleteResponse = await _client.SendAsync(
            CreateRequest(HttpMethod.Delete, location, ManagePermissions, updatedETag));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFoundResponse = await _client.SendAsync(CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ));
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var notFoundProblem = await notFoundResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        notFoundProblem!.Status.Should().Be((int)HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
        // Payload 在 PostgreSQL 中是 jsonb，无法直接翻译 LIKE，改在内存中过滤。
        var outbox = (await dbContext.OutboxMessages.AsNoTracking().ToListAsync())
            .Where(message => message.Payload.Contains(created.Id.ToString(), StringComparison.Ordinal))
            .ToList();
        outbox.Select(message => message.EventType).Should().BeEquivalentTo(
            OutboxMessage.LOCATION_CREATED,
            OutboxMessage.LOCATION_UPDATED,
            OutboxMessage.LOCATION_DELETED);
        outbox.Should().OnlyContain(message => message.PublishedAtUtc == null);
    }

    [Fact]
    public async Task Get_with_filters_returns_ordered_pages()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
        dbContext.ReferenceLocations.AddRange(Enumerable.Range(0, 25).Select(index => new ReferenceLocation
        {
            Id = Guid.NewGuid(),
            Type = ReferenceLocationType.Airport,
            Code = $"P{index:00}",
            Name = $"分页测试机场 {index:00}",
            CountryCode = "CN"
        }));
        await dbContext.SaveChangesAsync();
        var name = Uri.EscapeDataString("分页测试");

        using var firstRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/reference-data/airports?name={name}",
            ReferenceDataPermissions.READ);
        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationResponse>>();
        firstPage!.TotalCount.Should().Be(25);
        firstPage.Page.Should().Be(1);
        firstPage.PageSize.Should().Be(20);
        firstPage.Items.Should().HaveCount(20);
        firstPage.Items.Select(item => item.Code).Should().BeInAscendingOrder();

        using var lastRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/reference-data/airports?name={name}&page=3&pageSize=10",
            ReferenceDataPermissions.READ);
        var lastResponse = await _client.SendAsync(lastRequest);
        var lastPage = await lastResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationResponse>>();
        lastPage!.Items.Should().HaveCount(5);
        lastPage.Items.First().Code.Should().Be("P20");

        using var codeRequest = CreateRequest(
            HttpMethod.Get,
            "/api/reference-data/airports?code=p07",
            ReferenceDataPermissions.READ);
        var codeResponse = await _client.SendAsync(codeRequest);
        var codeResult = await codeResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationResponse>>();
        codeResult!.Items.Should().ContainSingle(item => item.Code == "P07");
    }

    [Fact]
    public async Task Get_with_invalid_query_returns_validation_problem()
    {
        var queries = new[]
        {
            "page=0",
            "pageSize=0",
            "pageSize=101",
            $"code={new string('A', 21)}",
            $"name={Uri.EscapeDataString(new string('名', 201))}"
        };

        foreach (var query in queries)
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"/api/reference-data/airports?{query}",
                ReferenceDataPermissions.READ);
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
            var content = await response.Content.ReadAsStringAsync();
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            problem.Should().NotBeNull();
            problem!.Errors.Should().NotBeEmpty(content);
        }
    }

    [Fact]
    public async Task OpenApi_document_describes_queries_and_bearer_authentication()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = document!.RootElement;
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Axiom ReferenceData API");
        var airportGet = root.GetProperty("paths")
            .GetProperty("/api/reference-data/airports")
            .GetProperty("get");
        var parameterNames = airportGet.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToList();
        parameterNames.Should().Contain(new[] { "Code", "Name", "Page", "PageSize" });
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().Should().Be("http");
        bearer.GetProperty("scheme").GetString().Should().Be("bearer");
        bearer.GetProperty("bearerFormat").GetString().Should().Be("JWT");
        root.GetProperty("security")[0].GetProperty("Bearer").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Write_requires_current_strong_etag()
    {
        var input = ValidAirport() with { Code = "lck", Name = "并发测试机场" };
        using var createRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/airports",
            ManagePermissions,
            input);
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var etag = createResponse.Headers.ETag!.ToString();
        var location = createResponse.Headers.Location!.OriginalString;
        var update = input with { Name = "并发测试机场（更新）" };

        using var missingRequest = CreateJsonRequest(HttpMethod.Put, location, ManagePermissions, update);
        var missingResponse = await _client.SendAsync(missingRequest);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        missingProblem!.Status.Should().Be((int)HttpStatusCode.PreconditionRequired);

        using var invalidRequest = CreateJsonRequest(
            HttpMethod.Put,
            location,
            ManagePermissions,
            update,
            "\"invalid\"");
        var invalidResponse = await _client.SendAsync(invalidRequest);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var invalidProblem = await invalidResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        invalidProblem!.Status.Should().Be((int)HttpStatusCode.BadRequest);

        var staleETag = $"\"{Guid.NewGuid():N}\"";
        using var staleUpdateRequest = CreateJsonRequest(
            HttpMethod.Put,
            location,
            ManagePermissions,
            update,
            staleETag);
        var staleUpdateResponse = await _client.SendAsync(staleUpdateRequest);
        staleUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var updateProblem = await staleUpdateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        updateProblem!.Status.Should().Be((int)HttpStatusCode.Conflict);

        using var staleDeleteRequest = CreateRequest(HttpMethod.Delete, location, ManagePermissions, staleETag);
        var staleDeleteResponse = await _client.SendAsync(staleDeleteRequest);
        staleDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var deleteProblem = await staleDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        deleteProblem!.Status.Should().Be((int)HttpStatusCode.Conflict);

        var getResponse = await _client.SendAsync(CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ));
        var unchanged = await getResponse.Content.ReadFromJsonAsync<ReferenceLocationResponse>();
        unchanged!.Name.Should().Be("并发测试机场");

        using var deleteRequest = CreateRequest(HttpMethod.Delete, location, ManagePermissions, etag);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_with_invalid_airport_code_returns_400()
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/airports",
            ManagePermissions,
            ValidAirport() with { Code = "INVALID" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    private static string ManagePermissions =>
        $"{ReferenceDataPermissions.READ},{ReferenceDataPermissions.MANAGE}";

    private static UpsertReferenceLocationRequest ValidAirport() => new(
        "sha",
        "上海浦东国际机场",
        "cn",
        "上海",
        31.1443,
        121.8083);

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri,
        string permissions,
        string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(TestAuthHandler.PERMISSIONS_HEADER, permissions);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string uri,
        string permissions,
        UpsertReferenceLocationRequest body,
        string? ifMatch = null)
    {
        var request = CreateRequest(method, uri, permissions, ifMatch);
        request.Content = JsonContent.Create(body);
        return request;
    }
}
