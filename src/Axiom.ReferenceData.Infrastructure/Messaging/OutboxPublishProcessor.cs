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
        List<OutboxMessage> messages;
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            // 测试用 InMemory 提供程序不支持原生 SQL：回退到普通查询（无认领语义）。
            messages = await dbContext.OutboxMessages
                .Where(message => message.PublishedAtUtc == null && message.NextAttemptAtUtc <= now)
                .OrderBy(message => message.OccurredAtUtc)
                .Take(BATCH_SIZE)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // FOR UPDATE SKIP LOCKED 认领：多副本同时跑发布器时各取各的批次，
            // 行锁保持到 SaveChanges 提交为止，避免重复发布与重试计数互相覆盖。
            messages = await dbContext.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_messages
                    WHERE "PublishedAtUtc" IS NULL AND "NextAttemptAtUtc" <= {now}
                    ORDER BY "OccurredAtUtc"
                    LIMIT {BATCH_SIZE}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
        }

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
