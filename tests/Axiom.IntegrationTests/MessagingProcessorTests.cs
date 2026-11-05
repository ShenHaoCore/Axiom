// 验证 Outbox Confirm 后标记、失败重试和 Inbox 持久化去重语义。
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Axiom.IntegrationTests;

public sealed class MessagingProcessorTests
{
    [Fact]
    public async Task Outbox_publish_marks_only_confirmed_messages()
    {
        var options = CreateOptions();
        var pending = CreateMessage();
        var published = CreateMessage();
        published.PublishedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using var arrangeContext = new ReferenceDataDbContext(options);
        arrangeContext.OutboxMessages.AddRange(pending, published);
        await arrangeContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        await using var context = new ReferenceDataDbContext(options);
        var processor = new OutboxPublishProcessor(context, publisher);
        var count = await processor.PublishPendingAsync();

        count.Should().Be(1);
        publisher.MessageIds.Should().ContainSingle().Which.Should().Be(pending.Id);
        (await context.OutboxMessages.SingleAsync(message => message.Id == pending.Id))
            .PublishedAtUtc.Should().NotBeNull();
        (await context.OutboxMessages.SingleAsync(message => message.Id == published.Id))
            .PublishedAtUtc.Should().Be(published.PublishedAtUtc);
    }

    [Fact]
    public async Task Outbox_publish_failure_leaves_message_pending()
    {
        var options = CreateOptions();
        var message = CreateMessage();
        await using var arrangeContext = new ReferenceDataDbContext(options);
        arrangeContext.OutboxMessages.Add(message);
        await arrangeContext.SaveChangesAsync();
        var publisher = new RecordingPublisher { ShouldFail = true };

        await using var context = new ReferenceDataDbContext(options);
        var processor = new OutboxPublishProcessor(context, publisher);
        var publish = () => processor.PublishPendingAsync();

        await publish.Should().ThrowAsync<InvalidOperationException>();
        (await context.OutboxMessages.SingleAsync(item => item.Id == message.Id))
            .PublishedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Inbox_duplicate_event_is_processed_once()
    {
        await using var context = new ReferenceDataDbContext(CreateOptions());
        var processor = new InboxProcessor(context);
        var eventId = Guid.NewGuid();

        var first = await processor.ProcessAsync(eventId, "test-consumer");
        var duplicate = await processor.ProcessAsync(eventId, "test-consumer");

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        (await context.InboxMessages.CountAsync()).Should().Be(1);
    }

    private static DbContextOptions<ReferenceDataDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ReferenceDataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static OutboxMessage CreateMessage() => new()
    {
        Id = Guid.NewGuid(),
        EventType = OutboxMessage.LOCATION_CREATED,
        Payload = "{}",
        OccurredAtUtc = DateTimeOffset.UtcNow
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
