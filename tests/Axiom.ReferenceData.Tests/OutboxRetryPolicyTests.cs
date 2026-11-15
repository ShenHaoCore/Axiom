// 验证 Outbox 失败后的退避间隔按指数增长并封顶。
using Axiom.ReferenceData.Infrastructure.Messaging;
using FluentAssertions;

namespace Axiom.ReferenceData.Tests;

public sealed class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(8, 256)]
    [InlineData(9, 300)]
    [InlineData(100, 300)]
    public void GetDelay_returns_capped_exponential_backoff(int attemptCount, double expectedSeconds)
    {
        OutboxRetryPolicy.GetDelay(attemptCount).TotalSeconds.Should().Be(expectedSeconds);
    }
}
