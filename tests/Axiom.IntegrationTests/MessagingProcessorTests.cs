// 验证 Outbox Confirm 后标记、失败重试和 Inbox 持久化去重语义。
using Axiom.ReferenceData.Infrastructure.Messaging;
using Axiom.ReferenceData.Infrastructure.EfCore;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Axiom.IntegrationTests;

public sealed class MessagingProcessorTests
{
    [Fact]
    public async Task Outbox_publish_marks_only_confirmed_messages()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        var options = testContext.Options;
        var pending = CreateMessage();
        var published = CreateMessage();
        published.PublishedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using var arrangeContext = new ReferenceDataDbContext(options);
        arrangeContext.OutboxMessages.AddRange(pending, published);
        await arrangeContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        await using var context = new ReferenceDataDbContext(options);
        var processor = new OutboxPublishProcessor(
            context,
            publisher,
            NullLogger<OutboxPublishProcessor>.Instance);
        var count = await processor.PublishPendingAsync();

        count.Should().Be(1);
        publisher.MessageIds.Should().ContainSingle().Which.Should().Be(pending.Id);
        (await context.OutboxMessages.SingleAsync(message => message.Id == pending.Id))
            .PublishedAtUtc.Should().NotBeNull();
        // PostgreSQL timestamptz 只保留微秒精度，按毫秒容差比较。
        (await context.OutboxMessages.SingleAsync(message => message.Id == published.Id))
            .PublishedAtUtc.Should().BeCloseTo(published.PublishedAtUtc!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Outbox_publish_failure_schedules_retry_with_backoff()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        var options = testContext.Options;
        var message = CreateMessage();
        await using var arrangeContext = new ReferenceDataDbContext(options);
        arrangeContext.OutboxMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var publisher = new RecordingPublisher { ShouldFail = true };

        await using var context = new ReferenceDataDbContext(options);
        var processor = new OutboxPublishProcessor(
            context,
            publisher,
            NullLogger<OutboxPublishProcessor>.Instance);

        var count = await processor.PublishPendingAsync();

        count.Should().Be(1);
        var stored = await context.OutboxMessages.SingleAsync(item => item.Id == message.Id);
        stored.PublishedAtUtc.Should().BeNull();
        stored.AttemptCount.Should().Be(1);
        stored.LastError.Should().Contain("Publisher Confirm");
        stored.NextAttemptAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
        publisher.MessageIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Outbox_skips_message_until_retry_delay_elapses()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        var options = testContext.Options;
        var message = CreateMessage();
        message.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
        await using var arrangeContext = new ReferenceDataDbContext(options);
        arrangeContext.OutboxMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        await using var context = new ReferenceDataDbContext(options);
        var processor = new OutboxPublishProcessor(
            context,
            publisher,
            NullLogger<OutboxPublishProcessor>.Instance);

        var count = await processor.PublishPendingAsync();

        count.Should().Be(0);
        publisher.MessageIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Inbox_duplicate_event_is_processed_once()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        await using var context = new ReferenceDataDbContext(testContext.Options);
        var processor = new InboxProcessor(context);
        var eventId = Guid.NewGuid();

        var first = await processor.ProcessAsync(eventId, "test-consumer");
        var duplicate = await processor.ProcessAsync(eventId, "test-consumer");

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        (await context.InboxMessages.CountAsync()).Should().Be(1);
    }

    private static OutboxMessage CreateMessage() => new()
    {
        Id = Guid.NewGuid(),
        EventType = OutboxMessage.LOCATION_CREATED,
        Payload = "{}",
        OccurredAtUtc = DateTimeOffset.UtcNow,
        NextAttemptAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class RecordingPublisher : IMessagePublisher
    {
        public List<Guid> MessageIds { get; } = [];
        public bool ShouldFail { get; init; }

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldFail)
            {
                throw new InvalidOperationException("模拟 Publisher Confirm 失败。");
            }

            MessageIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }
}
