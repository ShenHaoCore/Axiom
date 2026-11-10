// 为 RabbitMQ 发布与消费创建可关联的 OpenTelemetry 链路。
using System.Diagnostics;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

internal static class ReferenceDataTelemetry
{
    public static readonly ActivitySource Source = new("Axiom.ReferenceData.Api");
}
