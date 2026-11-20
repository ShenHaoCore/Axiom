// 网关调用传输层异常的统一包装：由 AuthenticatedApiClient 抛出，
// 供 GatewayUnavailablePageFilter 捕获并转成用户可读的内联错误，避免裸异常页丢表单。
namespace Axiom.Web.Clients;

public sealed class GatewayUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
