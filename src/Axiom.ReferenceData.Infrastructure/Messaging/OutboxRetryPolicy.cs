// 计算 Outbox 发布失败后的指数退避间隔，上限 5 分钟，避免 Broker 故障时高频重试。
namespace Axiom.ReferenceData.Infrastructure.Messaging;

public static class OutboxRetryPolicy
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    public static TimeSpan GetDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount, 1, 12);
        var seconds = Math.Min(Math.Pow(2, exponent), MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
