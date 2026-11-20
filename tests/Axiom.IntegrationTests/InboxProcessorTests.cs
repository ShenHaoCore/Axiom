// 验证消费者按 EventId + Consumer 组合实现幂等去重。
using Axiom.ReferenceData.Infrastructure.EfCore;
using Axiom.ReferenceData.Infrastructure.Messaging;
using FluentAssertions;

namespace Axiom.IntegrationTests;

public sealed class InboxProcessorTests
{
    [Fact]
    public async Task ProcessAsync_returns_false_for_duplicate_event()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        await using var dbContext = new ReferenceDataDbContext(testContext.Options);
        var processor = new InboxProcessor(dbContext);
        var eventId = Guid.NewGuid();

        (await processor.ProcessAsync(eventId, "consumer-a")).Should().BeTrue();
        (await processor.ProcessAsync(eventId, "consumer-a")).Should().BeFalse();
        (await processor.ProcessAsync(eventId, "consumer-b")).Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_consumers_process_same_event_exactly_once()
    {
        await using var testContext = await ReferenceDataTestContext.CreateAsync();
        var eventId = Guid.NewGuid();
        var consumerName = "consumer-race";

        // 每个消费者独立 DbContext（模拟多实例部署），并发处理同一事件。
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var dbContext = new ReferenceDataDbContext(testContext.Options);
            var processor = new InboxProcessor(dbContext);
            return await processor.ProcessAsync(eventId, consumerName);
        }));

        // 唯一约束 + 回查兜底：并发下恰有一个消费者成功登记。
        results.Should().ContainSingle(processed => processed);

        await using var verifyContext = new ReferenceDataDbContext(testContext.Options);
        // ContainSingle：库中恰好一条（多于一条会断言失败），且事件与消费者匹配。
        var stored = verifyContext.InboxMessages
            .Where(message => message.EventId == eventId && message.Consumer == consumerName)
            .ToList();
        stored.Should().ContainSingle();
        stored[0].EventId.Should().Be(eventId);
    }
}
