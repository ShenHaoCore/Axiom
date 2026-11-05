// 创建或更新新闻文章的请求 DTO。
namespace Axiom.News.Api.Models;

public sealed record UpsertNewsArticleRequest(
    string? Title,
    string? Summary,
    string? Content,
    string? Category,
    string? Author,
    string? Status);
