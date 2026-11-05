// 提供新闻文章 CRUD、筛选分页和乐观并发的 REST 端点。
using Axiom.Contracts.News;
using Axiom.News.Api.Data;
using Axiom.News.Api.Models;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.News.Api.Endpoints;

public static class NewsArticleEndpoints
{
    public const string READ_POLICY = "NewsRead";
    public const string MANAGE_POLICY = "NewsManage";

    public static void MapNewsArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/news/articles").WithTags("News");

        group.MapGet("/", ([AsParameters] NewsQueryParameters query, NewsDbContext dbContext,
                CancellationToken cancellationToken) => ListAsync(query, dbContext, cancellationToken))
            .RequireAuthorization(READ_POLICY)
            .Produces<PagedResponse<NewsArticleResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", (Guid id, NewsDbContext dbContext, HttpContext httpContext,
                CancellationToken cancellationToken) => GetAsync(id, dbContext, httpContext, cancellationToken))
            .RequireAuthorization(READ_POLICY)
            .Produces<NewsArticleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", (UpsertNewsArticleRequest? request, NewsDbContext dbContext,
                HttpContext httpContext, CancellationToken cancellationToken) =>
                CreateAsync(request, dbContext, httpContext, cancellationToken))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<NewsArticleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", (Guid id, UpsertNewsArticleRequest? request,
                NewsDbContext dbContext, HttpContext httpContext, CancellationToken cancellationToken) =>
                UpdateAsync(id, request, dbContext, httpContext, cancellationToken))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces<NewsArticleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapDelete("/{id:guid}", (Guid id, NewsDbContext dbContext, HttpContext httpContext,
                CancellationToken cancellationToken) =>
                DeleteAsync(id, dbContext, httpContext, cancellationToken))
            .RequireAuthorization(MANAGE_POLICY)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    private static async Task<IResult> ListAsync(
        NewsQueryParameters query,
        NewsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var articles = dbContext.NewsArticles.AsNoTracking();

        if (query.NormalizedCategory is { } category)
        {
            articles = articles.Where(article => article.Category == category);
        }

        if (query.NormalizedStatus is { } status &&
            Enum.TryParse<NewsArticleStatus>(status, true, out var parsedStatus))
        {
            articles = articles.Where(article => article.Status == parsedStatus);
        }

        if (query.NormalizedKeyword is { } keyword)
        {
            articles = articles.Where(article =>
                article.Title.Contains(keyword) ||
                (article.Summary != null && article.Summary.Contains(keyword)) ||
                article.Content.Contains(keyword));
        }

        var totalCount = await articles.CountAsync(cancellationToken);
        var items = await articles
            .OrderByDescending(article => article.PublishedAt ?? article.CreatedAt)
            .ThenBy(article => article.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var response = new PagedResponse<NewsArticleResponse>(
            items.Select(article => article.ToResponse()).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        NewsDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var article = await dbContext.NewsArticles.AsNoTracking()
            .SingleOrDefaultAsync(article => article.Id == id, cancellationToken);
        if (article is null)
        {
            return ArticleNotFound();
        }

        SetETag(httpContext, article.ConcurrencyToken);
        return Results.Ok(article.ToResponse());
    }

    private static async Task<IResult> CreateAsync(
        UpsertNewsArticleRequest? request,
        NewsDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = NewsArticleInputValidator.Validate(request, isCreate: true);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var status = ParseStatus(request.Status, NewsArticleStatus.Draft);
        var article = new NewsArticle
        {
            Id = Guid.NewGuid(),
            Title = request.Title!.Trim(),
            Summary = NormalizeNullable(request.Summary),
            Content = request.Content!.Trim(),
            Category = request.Category!.Trim(),
            Author = request.Author!.Trim(),
            Status = status,
            PublishedAt = status == NewsArticleStatus.Published ? DateTimeOffset.UtcNow : null
        };

        dbContext.NewsArticles.Add(article);
        await dbContext.SaveChangesAsync(cancellationToken);
        SetETag(httpContext, article.ConcurrencyToken);
        return Results.Created($"/api/news/articles/{article.Id}", article.ToResponse());
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpsertNewsArticleRequest? request,
        NewsDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "请求正文不能为空。");
        }

        var errors = NewsArticleInputValidator.Validate(request, isCreate: false);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var article = await dbContext.NewsArticles
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return ArticleNotFound();
        }

        if (!TryGetExpectedVersion(httpContext, out var expectedVersion, out var preconditionError))
        {
            return preconditionError!;
        }

        dbContext.Entry(article).Property(a => a.ConcurrencyToken).OriginalValue = expectedVersion;

        if (request.Title is not null) article.Title = request.Title.Trim();
        if (request.Summary is not null) article.Summary = NormalizeNullable(request.Summary);
        if (request.Content is not null) article.Content = request.Content.Trim();
        if (request.Category is not null) article.Category = request.Category.Trim();
        if (request.Author is not null) article.Author = request.Author.Trim();
        if (request.Status is { } statusStr)
        {
            var newStatus = ParseStatus(statusStr, article.Status);
            if (newStatus == NewsArticleStatus.Published && article.Status != NewsArticleStatus.Published)
            {
                article.PublishedAt = DateTimeOffset.UtcNow;
            }

            article.Status = newStatus;
        }

        article.ConcurrencyToken = Guid.NewGuid();
        article.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        SetETag(httpContext, article.ConcurrencyToken);
        return Results.Ok(article.ToResponse());
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        NewsDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var article = await dbContext.NewsArticles
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return ArticleNotFound();
        }

        if (!TryGetExpectedVersion(httpContext, out var expectedVersion, out var preconditionError))
        {
            return preconditionError!;
        }

        dbContext.Entry(article).Property(a => a.ConcurrencyToken).OriginalValue = expectedVersion;
        dbContext.NewsArticles.Remove(article);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }

        return Results.NoContent();
    }

    private static bool TryGetExpectedVersion(
        HttpContext httpContext,
        out Guid expectedVersion,
        out IResult? error)
    {
        expectedVersion = default;
        error = null;
        var etag = httpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(etag))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "缺少 If-Match 请求头。");
            return false;
        }

        if (etag.Length < 3 || etag[0] != '"' || etag[^1] != '"' ||
            !Guid.TryParseExact(etag[1..^1], "N", out expectedVersion))
        {
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "If-Match 必须使用当前资源返回的强 ETag。");
            return false;
        }

        return true;
    }

    private static void SetETag(HttpContext httpContext, Guid version) =>
        httpContext.Response.Headers.ETag = $"\"{version:N}\"";

    private static NewsArticleStatus ParseStatus(string? value, NewsArticleStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse<NewsArticleStatus>(value.Trim(), true, out var parsed) ? parsed : fallback;
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult ArticleNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "指定的新闻不存在。");

    private static IResult ConcurrencyConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "新闻已被其他请求修改，请重新获取后再操作。");
}
