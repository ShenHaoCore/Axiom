// 持久化消费者已处理的事件标识，避免至少一次投递造成重复处理。
namespace Axiom.ReferenceData.Infrastructure.Messaging;

public sealed class InboxMessage
{
    public Guid EventId { get; set; }
    public required string Consumer { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
}
