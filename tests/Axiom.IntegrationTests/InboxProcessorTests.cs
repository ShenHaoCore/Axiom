// 验证消费者按 EventId + Consumer 组合实现幂等去重。
using Axiom.ReferenceData.Api.Data;
using Axiom.ReferenceData.Api.Messaging;
using FluentAssertions;

using Axiom.ReferenceData.Infrastructure.EfCore;

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
}
