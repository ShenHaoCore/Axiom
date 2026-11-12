// 验证基础数据 API 的鉴权行为和完整 CRUD 链路。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Axiom.ReferenceData.Infrastructure.Messaging;
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Infrastructure.EfCore;
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
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationDto>>();
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
        var created = await createResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
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
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
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
    public async Task Post_region_with_only_read_permission_returns_403()
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            "/api/geo-divisions",
            ReferenceDataPermissions.READ,
            new CreateGeoDivisionInput(
                1,
                "ZZ",
                Code: Guid.NewGuid().ToString("N"),
                Name: "只读权限回归国家"));

        var response = await _client.SendAsync(request);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, content);
    }

    [Fact]
    public async Task Manage_permission_supports_complete_region_crud()
    {
        var testId = Guid.NewGuid();
        var suffix = testId.ToString("N")[..8].ToUpperInvariant();
        var countryCode = $"X{(char)('A' + testId.ToByteArray()[0] % 26)}";
        var subdivisionCode = $"A{suffix}";
        var cityCode = $"CITY-{suffix}";
        var countryInput = new CreateGeoDivisionInput(
            1,
            countryCode,
            Code: countryCode,
            Name: $"回归国家 {suffix}");
        var country = await CreateRegion(countryInput);
        country.Level.Should().Be(1);
        country.Kind.Should().Be("other", "未提供 Kind 时按深度兜底为 other");
        country.CountryCode.Should().Be(countryCode);
        country.ParentId.Should().BeNull();

        var admin1Input = new CreateGeoDivisionInput(
            2,
            countryCode,
            SubdivisionCode: subdivisionCode,
            Code: subdivisionCode,
            Name: $"回归省州 {suffix}",
            ParentId: country.Id);
        var admin1 = await CreateRegion(admin1Input with { Kind = "municipality" });
        admin1.Level.Should().Be(2);
        admin1.Kind.Should().Be("municipality");
        admin1.ParentId.Should().Be(country.Id);

        var cityInput = new CreateGeoDivisionInput(
            3,
            countryCode,
            SubdivisionCode: subdivisionCode,
            Code: cityCode,
            Name: $"回归城市 {suffix}",
            AsciiName: $"Regression City {suffix}",
            LocalName: $"本地城市 {suffix}",
            Latitude: 31.2,
            Longitude: 121.5,
            Population: 1234,
            ParentId: admin1.Id);
        var city = await CreateRegion(cityInput);
        city.Level.Should().Be(3);
        city.ParentId.Should().Be(admin1.Id);
        city.GeoNameId.Should().BeNull();

        using var duplicateRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/geo-divisions",
            ManagePermissions,
            cityInput);
        var duplicateResponse = await _client.SendAsync(duplicateRequest);
        var duplicateContent = await duplicateResponse.Content.ReadAsStringAsync();
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict, duplicateContent);
        var duplicateProblem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        duplicateProblem.Should().NotBeNull(duplicateContent);
        duplicateProblem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        duplicateProblem.Title.Should().NotBeNullOrWhiteSpace();

        using var detailRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/{city.Id}",
            ReferenceDataPermissions.READ);
        var detailResponse = await _client.SendAsync(detailRequest);
        var detailContent = await detailResponse.Content.ReadAsStringAsync();
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK, detailContent);
        var detail = await detailResponse.Content.ReadFromJsonAsync<GeoDivisionDto>();
        detail.Should().NotBeNull(detailContent);
        detail!.Should().BeEquivalentTo(city);

        using var listRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/?countryCode={countryCode}&page=1&pageSize=50",
            ReferenceDataPermissions.READ);
        var listResponse = await _client.SendAsync(listRequest);
        var listContent = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listContent);
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResponse<GeoDivisionDto>>();
        list.Should().NotBeNull(listContent);
        list!.TotalCount.Should().Be(3);
        list.Items.Select(item => item.Id).Should().BeEquivalentTo(new[] { country.Id, admin1.Id, city.Id });

        using var updateRequest = CreateJsonRequest(
            HttpMethod.Put,
            $"/api/geo-divisions/{city.Id}",
            ManagePermissions,
            new UpdateGeoDivisionInput(
                Name: $"回归城市（已更新）{suffix}",
                Code: $"{cityCode}-UPDATED",
                Latitude: 30.2,
                Longitude: 120.5,
                Population: 5678));
        var updateResponse = await _client.SendAsync(updateRequest);
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateContent);
        var updated = await updateResponse.Content.ReadFromJsonAsync<GeoDivisionDto>();
        updated.Should().NotBeNull(updateContent);
        updated!.Name.Should().Be($"回归城市（已更新）{suffix}");
        updated.Code.Should().Be($"{cityCode}-UPDATED");
        updated.Latitude.Should().Be(30.2);
        updated.Longitude.Should().Be(120.5);
        updated.Population.Should().Be(5678);
        updated.Level.Should().Be(3);
        updated.CountryCode.Should().Be(countryCode);
        updated.SubdivisionCode.Should().Be(subdivisionCode);
        updated.ParentId.Should().Be(admin1.Id);

        using var treeRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/tree?countryCode={countryCode}&depth=3",
            ReferenceDataPermissions.READ);
        var treeResponse = await _client.SendAsync(treeRequest);
        var treeContent = await treeResponse.Content.ReadAsStringAsync();
        treeResponse.StatusCode.Should().Be(HttpStatusCode.OK, treeContent);
        var tree = await treeResponse.Content.ReadFromJsonAsync<GeoDivisionTreeDto>();
        tree.Should().NotBeNull(treeContent);
        tree!.Id.Should().Be(country.Id);
        var admin1Node = tree.Children.Should().ContainSingle().Which;
        admin1Node.Id.Should().Be(admin1.Id);
        var cityNode = admin1Node.Children.Should().ContainSingle().Which;
        cityNode.Id.Should().Be(city.Id);
        cityNode.Name.Should().Be(updated.Name);
        cityNode.Code.Should().Be(updated.Code);

        // 单级子级接口：惰性树数据源，附带下级数量与同名下级数量。
        using var childrenRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/{country.Id}/children",
            ReferenceDataPermissions.READ);
        var childrenResponse = await _client.SendAsync(childrenRequest);
        var childrenContent = await childrenResponse.Content.ReadAsStringAsync();
        childrenResponse.StatusCode.Should().Be(HttpStatusCode.OK, childrenContent);
        var children = await childrenResponse.Content.ReadFromJsonAsync<List<GeoDivisionChildDto>>();
        children.Should().NotBeNull(childrenContent);
        var childAdmin1 = children!.Should().ContainSingle().Which;
        childAdmin1.Id.Should().Be(admin1.Id);
        childAdmin1.ChildCount.Should().Be(1);
        childAdmin1.SameNameChildCount.Should().Be(0);

        using var cityChildrenRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/{admin1.Id}/children",
            ReferenceDataPermissions.READ);
        var cityChildrenResponse = await _client.SendAsync(cityChildrenRequest);
        var cityChildrenContent = await cityChildrenResponse.Content.ReadAsStringAsync();
        cityChildrenResponse.StatusCode.Should().Be(HttpStatusCode.OK, cityChildrenContent);
        var cityChildren = await cityChildrenResponse.Content.ReadFromJsonAsync<List<GeoDivisionChildDto>>();
        cityChildren!.Should().ContainSingle().Which.Id.Should().Be(city.Id);

        using var missingChildrenRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/geo-divisions/{Guid.NewGuid()}/children",
            ReferenceDataPermissions.READ);
        (await _client.SendAsync(missingChildrenRequest)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var protectedDeleteRequest = CreateRequest(
            HttpMethod.Delete,
            $"/api/geo-divisions/{admin1.Id}",
            ManagePermissions);
        var protectedDeleteResponse = await _client.SendAsync(protectedDeleteRequest);
        var protectedDeleteContent = await protectedDeleteResponse.Content.ReadAsStringAsync();
        protectedDeleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, protectedDeleteContent);
        var protectedDeleteProblem = await protectedDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        protectedDeleteProblem.Should().NotBeNull(protectedDeleteContent);
        protectedDeleteProblem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        protectedDeleteProblem.Title.Should().NotBeNullOrWhiteSpace();

        await DeleteRegion(city.Id);
        await DeleteRegion(admin1.Id);
        await DeleteRegion(country.Id);

        foreach (var id in new[] { city.Id, admin1.Id, country.Id })
        {
            using var notFoundRequest = CreateRequest(
                HttpMethod.Get,
                $"/api/geo-divisions/{id}",
                ReferenceDataPermissions.READ);
            var notFoundResponse = await _client.SendAsync(notFoundRequest);
            var notFoundContent = await notFoundResponse.Content.ReadAsStringAsync();
            notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, notFoundContent);
        }

        async Task<GeoDivisionDto> CreateRegion(CreateGeoDivisionInput input)
        {
            using var request = CreateJsonRequest(HttpMethod.Post, "/api/geo-divisions", ManagePermissions, input);
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Created, content);
            var created = await response.Content.ReadFromJsonAsync<GeoDivisionDto>();
            created.Should().NotBeNull(content);
            return created!;
        }

        async Task DeleteRegion(Guid id)
        {
            using var request = CreateRequest(HttpMethod.Delete, $"/api/geo-divisions/{id}", ManagePermissions);
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, content);
        }
    }

    [Fact]
    public async Task Get_with_filters_returns_ordered_pages()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
        dbContext.ReferenceLocations.AddRange(Enumerable.Range(0, 25).Select(index =>
            ReferenceLocation.Create(
                ReferenceLocationType.Airport,
                $"AA{(char)('A' + index)}",
                $"分页测试机场 {index:00}",
                "CN")));
        await dbContext.SaveChangesAsync();
        var name = Uri.EscapeDataString("分页测试");

        using var firstRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/reference-data/airports?name={name}",
            ReferenceDataPermissions.READ);
        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationDto>>();
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
        var lastPage = await lastResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationDto>>();
        lastPage!.Items.Should().HaveCount(5);
        lastPage.Items.First().Code.Should().Be("AAU");

        using var codeRequest = CreateRequest(
            HttpMethod.Get,
            "/api/reference-data/airports?code=aah",
            ReferenceDataPermissions.READ);
        var codeResponse = await _client.SendAsync(codeRequest);
        var codeResult = await codeResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceLocationDto>>();
        codeResult!.Items.Should().ContainSingle(item => item.Code == "AAH");
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
        var unchanged = await getResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
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

    [Fact]
    public async Task Translations_are_merged_by_request_culture_and_fall_back_to_default()
    {
        var input = ValidAirport() with { Code = "TRN", Name = "翻译测试机场" };
        using var createRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/airports",
            ManagePermissions,
            input);
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = createResponse.Headers.Location!.OriginalString;

        using var saveRequest = CreateRequest(HttpMethod.Put, $"{location}/translations/en", ManagePermissions);
        saveRequest.Content = JsonContent.Create(
            new SaveTranslationsInput(new Dictionary<string, string> { ["Name"] = "Translation Test Airport" }));
        var saveResponse = await _client.SendAsync(saveRequest);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await saveResponse.Content.ReadFromJsonAsync<TranslationDto>();
        saved!.Values["Name"].Should().Be("Translation Test Airport");

        var defaultResponse = await _client.SendAsync(
            CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ));
        var defaultDto = await defaultResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
        defaultDto!.Name.Should().Be("翻译测试机场");

        using var englishRequest = CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ);
        englishRequest.Headers.Add("Accept-Language", "en");
        var englishResponse = await _client.SendAsync(englishRequest);
        var englishDto = await englishResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
        englishDto!.Name.Should().Be("Translation Test Airport");

        using var deleteTranslationRequest =
            CreateRequest(HttpMethod.Delete, $"{location}/translations/en", ManagePermissions);
        var deleteTranslationResponse = await _client.SendAsync(deleteTranslationRequest);
        deleteTranslationResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var afterDeleteRequest = CreateRequest(HttpMethod.Get, location, ReferenceDataPermissions.READ);
        afterDeleteRequest.Headers.Add("Accept-Language", "en");
        var afterDeleteResponse = await _client.SendAsync(afterDeleteRequest);
        var afterDeleteDto = await afterDeleteResponse.Content.ReadFromJsonAsync<ReferenceLocationDto>();
        afterDeleteDto!.Name.Should().Be("翻译测试机场");
    }

    [Fact]
    public async Task Enum_dictionary_supports_crud_kind_validation_and_in_use_delete()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();
        var category = $"test.category.{suffix}";
        var code = $"code_{suffix}";

        using var createRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/reference-data/enums",
            ManagePermissions,
            new CreateReferenceEnumInput(category, code, "测试枚举", 10));
        var createResponse = await _client.SendAsync(createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, createContent);
        var created = await createResponse.Content.ReadFromJsonAsync<ReferenceEnumDto>();
        created.Should().NotBeNull();
        created!.Category.Should().Be(category);
        created.Code.Should().Be(code);

        using var listRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/reference-data/enums?category={Uri.EscapeDataString(category)}",
            ReferenceDataPermissions.READ);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumDto>>();
        listed!.Items.Should().ContainSingle(item => item.Id == created.Id);

        using var updateRequest = CreateJsonRequest(
            HttpMethod.Put,
            $"/api/reference-data/enums/{created.Id}",
            ManagePermissions,
            new UpdateReferenceEnumInput("测试枚举改名", 20));
        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReferenceEnumDto>();
        updated!.Name.Should().Be("测试枚举改名");
        updated.SortOrder.Should().Be(20);

        var countryCode = $"T{(char)('A' + suffix[0] % 26)}";
        using var unknownKindRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/geo-divisions",
            ManagePermissions,
            new CreateGeoDivisionInput(1, countryCode, Code: countryCode, Name: "未知 Kind 国家", Kind: "not_a_real_kind"));
        var unknownKindResponse = await _client.SendAsync(unknownKindRequest);
        unknownKindResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var countryRequest = CreateJsonRequest(
            HttpMethod.Post,
            "/api/geo-divisions",
            ManagePermissions,
            new CreateGeoDivisionInput(1, countryCode, Code: countryCode, Name: "引用 Kind 国家", Kind: "country"));
        var countryResponse = await _client.SendAsync(countryRequest);
        var countryContent = await countryResponse.Content.ReadAsStringAsync();
        countryResponse.StatusCode.Should().Be(HttpStatusCode.Created, countryContent);
        var country = await countryResponse.Content.ReadFromJsonAsync<GeoDivisionDto>();

        using var listKinds = CreateRequest(
            HttpMethod.Get,
            "/api/reference-data/enums?category=geo_division.kind&pageSize=100",
            ReferenceDataPermissions.READ);
        var kindsResponse = await _client.SendAsync(listKinds);
        kindsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var kinds = await kindsResponse.Content.ReadFromJsonAsync<PagedResponse<ReferenceEnumDto>>();
        var countryKind = kinds!.Items.Single(item => item.Code == "country");

        using var deleteInUseKindRequest = CreateRequest(
            HttpMethod.Delete,
            $"/api/reference-data/enums/{countryKind.Id}",
            ManagePermissions);
        var deleteInUseKindResponse = await _client.SendAsync(deleteInUseKindRequest);
        deleteInUseKindResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var deleteCountry = CreateRequest(
            HttpMethod.Delete,
            $"/api/geo-divisions/{country!.Id}",
            ManagePermissions);
        (await _client.SendAsync(deleteCountry)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var deleteEnumRequest = CreateRequest(
            HttpMethod.Delete,
            $"/api/reference-data/enums/{created.Id}",
            ManagePermissions);
        (await _client.SendAsync(deleteEnumRequest)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static string ManagePermissions =>
        $"{ReferenceDataPermissions.READ},{ReferenceDataPermissions.MANAGE}";

    private static UpsertReferenceLocationInput ValidAirport() => new(
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

    private static HttpRequestMessage CreateJsonRequest<T>(
        HttpMethod method,
        string uri,
        string permissions,
        T body,
        string? ifMatch = null)
    {
        var request = CreateRequest(method, uri, permissions, ifMatch);
        request.Content = JsonContent.Create(body);
        return request;
    }
}
