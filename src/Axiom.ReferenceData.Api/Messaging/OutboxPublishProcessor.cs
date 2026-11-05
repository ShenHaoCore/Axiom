// 逐条发布待处理 Outbox，并仅在 Broker Confirm 后保存发布状态。
using Axiom.ReferenceData.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Api.Messaging;

public sealed class OutboxPublishProcessor(
    ReferenceDataDbContext dbContext,
    IMessagePublisher publisher)
{
    private const int BATCH_SIZE = 50;

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var messages = await dbContext.OutboxMessages
            .Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BATCH_SIZE)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await publisher.PublishAsync(message, cancellationToken);
            message.PublishedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }
}
