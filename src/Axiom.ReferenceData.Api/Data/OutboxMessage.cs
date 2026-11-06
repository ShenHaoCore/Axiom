// 将地点变更事件与业务数据保存在同一事务中，支持至少一次发布。
using System.Text.Json;
using Axiom.ReferenceData.Api.Models;

namespace Axiom.ReferenceData.Api.Data;

public sealed class OutboxMessage
{
    public const string LOCATION_CREATED = "referencedata.location.created.v1";
    public const string LOCATION_UPDATED = "referencedata.location.updated.v1";
    public const string LOCATION_DELETED = "referencedata.location.deleted.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }

    public static OutboxMessage FromLocation(ReferenceLocation location, string eventType)
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var payload = new
        {
            EventId = eventId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            Location = location.ToResponse()
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
