// 定义集成事件发布边界，便于业务处理与消息代理解耦。
using Axiom.ReferenceData.Infrastructure.Messaging;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
