// ReferenceLocation 仓储 EF Core 实现（Infrastructure 层）。
// 负责：并发令牌设置（DbUpdateConcurrencyException → 领域异常）、
//       Outbox 消息在同一事务内写入（保证一致性）。
using System.Text.Json;
using Axiom.ReferenceData.Infrastructure.Messaging; // OutboxMessage
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace Axiom.ReferenceData.Infrastructure.EfCore;

public sealed class ReferenceLocationRepository(ReferenceDataDbContext dbContext)
    : IReferenceLocationRepository
{
    public async Task<ReferenceLocation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.ReferenceLocations.SingleOrDefaultAsync(l => l.Id == id, ct);

    public async Task<ReferenceLocation?> GetByIdAsNoTrackingAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.ReferenceLocations.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, ct);

    public async Task<PagedResponse<ReferenceLocation>> ListByTypeAsync(
        ReferenceLocationType type,
        string? code,
        string? name,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = dbContext.ReferenceLocations.AsNoTracking().Where(l => l.Type == type);
        if (code is { } c) query = query.Where(l => l.Code == c);
        if (name is { } n)
        {
            // 双侧 Lower + LIKE：PostgreSQL 下实现大小写不敏感（等价 ILIKE），InMemory 测试同样可翻译；
            // 通配符转义后用户输入不再被解释为模式。
            var pattern = $"%{LikePattern.Escape(n.Trim().ToLowerInvariant())}%";
            query = query.Where(l => EF.Functions.Like(l.Name.ToLowerInvariant(), pattern));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(l => l.Code)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ReferenceLocation>(items, totalCount, page, pageSize);
    }

    public async Task<bool> CodeExistsAsync(
        ReferenceLocationType type,
        string code,
        Guid? excludedId,
        CancellationToken ct = default) =>
        await dbContext.ReferenceLocations.AnyAsync(
            l => l.Type == type && l.Code == code && l.Id != excludedId, ct);

    public async Task AddAsync(ReferenceLocation location, CancellationToken ct = default)
    {
        dbContext.ReferenceLocations.Add(location);
        dbContext.OutboxMessages.Add(CreateOutboxMessage(location, OutboxMessage.LOCATION_CREATED));
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (PostgresExceptions.IsUniqueViolation(exception))
        {
            // 前查与提交之间的并发插入竞态：以数据库唯一索引为准，映射为业务冲突。
            throw new ReferenceLocationCodeConflictException(location.Code, (int)location.Type);
        }
    }

    public async Task UpdateWithETagAsync(
        ReferenceLocation location,
        Guid expectedVersion,
        CancellationToken ct = default)
    {
        dbContext.Entry(location).Property(l => l.ConcurrencyToken).OriginalValue = expectedVersion;

        try
        {
            dbContext.OutboxMessages.Add(CreateOutboxMessage(location, OutboxMessage.LOCATION_UPDATED));
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReferenceLocationConcurrencyConflictException(location.Id);
        }
        catch (DbUpdateException exception) when (PostgresExceptions.IsUniqueViolation(exception))
        {
            // 并发换码竞态：唯一索引兜底，映射为业务冲突而非 500。
            throw new ReferenceLocationCodeConflictException(location.Code, (int)location.Type);
        }
    }

    public async Task RemoveWithETagAsync(
        ReferenceLocation location,
        Guid expectedVersion,
        CancellationToken ct = default)
    {
        dbContext.Entry(location).Property(l => l.ConcurrencyToken).OriginalValue = expectedVersion;

        try
        {
            dbContext.OutboxMessages.Add(CreateOutboxMessage(location, OutboxMessage.LOCATION_DELETED));
            dbContext.ReferenceLocations.Remove(location);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReferenceLocationConcurrencyConflictException(location.Id);
        }
    }

    public async Task<List<ReferenceLocationTranslation>> ListTranslationsAsync(
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<string> cultures,
        CancellationToken ct = default)
    {
        if (locationIds.Count == 0 || cultures.Count == 0)
        {
            return [];
        }

        var ids = locationIds.ToArray();
        var cultureTags = cultures.ToArray();

        return await dbContext.ReferenceLocationTranslations
            .AsNoTracking()
            .Where(translation => ids.Contains(translation.ReferenceLocationId) && cultureTags.Contains(translation.Culture))
            .ToListAsync(ct);
    }

    public async Task<List<ReferenceLocationTranslation>> GetTranslationsAsync(
        Guid locationId,
        string culture,
        CancellationToken ct = default) =>
        await dbContext.ReferenceLocationTranslations
            .AsNoTracking()
            .Where(translation => translation.ReferenceLocationId == locationId && translation.Culture == culture)
            .ToListAsync(ct);

    public async Task ReplaceTranslationsAsync(
        Guid locationId,
        string culture,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        var existing = await dbContext.ReferenceLocationTranslations
            .Where(translation => translation.ReferenceLocationId == locationId && translation.Culture == culture)
            .ToDictionaryAsync(translation => translation.Field, ct);

        foreach (var (field, value) in values)
        {
            if (existing.TryGetValue(field, out var translation))
            {
                translation.ChangeValue(value);
            }
            else
            {
                dbContext.ReferenceLocationTranslations.Add(
                    new ReferenceLocationTranslation(locationId, culture, field, value));
            }
        }

        var removed = existing
            .Where(entry => !values.ContainsKey(entry.Key))
            .Select(entry => entry.Value)
            .ToArray();

        dbContext.ReferenceLocationTranslations.RemoveRange(removed);

        // 译文属于资源表示的一部分（GET 会按语言回退链合并）：刷新主表并发令牌并复用
        // LOCATION_UPDATED 事件，保证 If-Match 与事件流覆盖“主表+译文”的完整表示。
        var location = await dbContext.ReferenceLocations.FirstOrDefaultAsync(l => l.Id == locationId, ct)
            ?? throw new ReferenceLocationNotFoundException(locationId);
        dbContext.Entry(location).Property(l => l.ConcurrencyToken).CurrentValue = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateOutboxMessage(location, OutboxMessage.LOCATION_UPDATED));

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteTranslationsAsync(Guid locationId, string culture, CancellationToken ct = default)
    {
        var existing = await dbContext.ReferenceLocationTranslations
            .Where(translation => translation.ReferenceLocationId == locationId && translation.Culture == culture)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            return false;
        }

        dbContext.ReferenceLocationTranslations.RemoveRange(existing);

        // 与 ReplaceTranslationsAsync 同理：表示发生变化，刷新并发令牌并发出更新事件。
        var location = await dbContext.ReferenceLocations.FirstOrDefaultAsync(l => l.Id == locationId, ct)
            ?? throw new ReferenceLocationNotFoundException(locationId);
        dbContext.Entry(location).Property(l => l.ConcurrencyToken).CurrentValue = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateOutboxMessage(location, OutboxMessage.LOCATION_UPDATED));

        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    // ---------- 私有：构造 OutboxMessage ----------

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static OutboxMessage CreateOutboxMessage(ReferenceLocation location, string eventType)
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = DateTimeOffset.UtcNow;

        // 直接构造 JSON payload，避免依赖 Application 层的 ReferenceLocationMapper。
        var payload = new
        {
            EventId = eventId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            Location = new
            {
                location.Id,
                Type = location.Type.ToString(),
                location.Code,
                location.Name,
                location.CountryCode,
                location.City,
                location.Latitude,
                location.Longitude
            }
        };

        return new OutboxMessage
        {
            Id = eventId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            OccurredAtUtc = occurredAtUtc,
            NextAttemptAtUtc = occurredAtUtc
        };
    }
}
