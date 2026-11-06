// BFF 转发给新闻服务的请求正文。
namespace Axiom.Bff.Contracts;

public sealed record NewsInput(
    string? Title,
    string? Summary,
    string? Content,
    string? Category,
    string? Author,
    string? Status);
