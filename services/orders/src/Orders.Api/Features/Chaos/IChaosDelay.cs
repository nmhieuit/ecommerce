namespace Orders.Api.Features.Chaos;

/// <summary>
/// Seam between <see cref="ChaosLatencyInjectionMiddleware"/> and the actual wait, so unit tests can
/// record what delay was requested without a test run ever waiting on it (research.md Quyết định 4).
/// </summary>
public interface IChaosDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

/// <summary>The real implementation, registered as the singleton this middleware uses in production.</summary>
public sealed class SystemChaosDelay : IChaosDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
