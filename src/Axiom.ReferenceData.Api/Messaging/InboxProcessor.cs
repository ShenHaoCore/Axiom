// 通过数据库唯一键实现消费者持久化幂等去重。
using Axiom.ReferenceData.Api.Data;
using Microsoft.EntityFrameworkCore;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Api.Messaging;

public sealed class InboxProcessor(ReferenceDataDbContext dbContext)
{
    public async Task<bool> ProcessAsync(
        Guid eventId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(eventId, consumer, cancellationToken))
        {
            return false;
        }

        var message = new InboxMessage
        {
            EventId = eventId,
            Consumer = consumer,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.InboxMessages.Add(message);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(message).State = EntityState.Detached;
            if (await ExistsAsync(eventId, consumer, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    private Task<bool> ExistsAsync(Guid eventId, string consumer, CancellationToken cancellationToken) =>
        dbContext.InboxMessages.AsNoTracking()
            .AnyAsync(item => item.EventId == eventId && item.Consumer == consumer, cancellationToken);
}
