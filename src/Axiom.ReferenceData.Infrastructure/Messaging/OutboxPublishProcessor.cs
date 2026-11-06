// 逐条发布待处理 Outbox；单条失败时记录错误并按指数退避安排下次重试，不影响同批其他消息。
using Axiom.ReferenceData.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Messaging;

public sealed class OutboxPublishProcessor(
    ReferenceDataDbContext dbContext,
    IMessagePublisher publisher,
    ILogger<OutboxPublishProcessor> logger)
{
    private const int BATCH_SIZE = 50;
    private const int MAX_ERROR_LENGTH = 500;

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.OutboxMessages
            .Where(message => message.PublishedAtUtc == null && message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BATCH_SIZE)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.PublishedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = Truncate(exception.Message);
                message.NextAttemptAtUtc = DateTimeOffset.UtcNow
                    + OutboxRetryPolicy.GetDelay(message.AttemptCount);
                logger.LogWarning(
                    exception,
                    "Outbox 消息 {MessageId} 第 {AttemptCount} 次发布失败，{DelaySeconds} 秒后重试。",
                    message.Id,
                    message.AttemptCount,
                    OutboxRetryPolicy.GetDelay(message.AttemptCount).TotalSeconds);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }

    private static string Truncate(string value) =>
        value.Length <= MAX_ERROR_LENGTH ? value : value[..MAX_ERROR_LENGTH];
}
