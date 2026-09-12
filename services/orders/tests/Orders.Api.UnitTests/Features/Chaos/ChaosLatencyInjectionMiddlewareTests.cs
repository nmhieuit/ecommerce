using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orders.Api.Features.Chaos;

namespace Orders.Api.UnitTests.Features.Chaos;

/// <summary>
/// specs/025-chaos-pod-kill-latency/contracts/chaos-latency-injection-contract.md — the invariants
/// a chaos exercise depends on being true before <see cref="ChaosLatencyInjectionMiddleware"/> is
/// ever pointed at a real cluster. Uses <see cref="RecordingChaosDelay"/> instead of a real
/// <c>Task.Delay</c> so these run instantly and deterministically (research.md Quyết định 4).
/// </summary>
public class ChaosLatencyInjectionMiddlewareTests
{
    private static readonly IOptions<ChaosOptions> Enabled =
        Options.Create(new ChaosOptions { AllowLatencyInjection = true });

    private static readonly IOptions<ChaosOptions> Disabled =
        Options.Create(new ChaosOptions { AllowLatencyInjection = false });

    [Fact]
    public async Task InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Disabled);

        Assert.Null(delay.RequestedDuration);
    }

    [Fact]
    public async Task InvokeAsync_EnabledWithoutHeader_DoesNotDelay()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Enabled);

        Assert.Null(delay.RequestedDuration);
    }

    [Fact]
    public async Task InvokeAsync_EnabledWithValidHeader_DelaysByRequestedAmount()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Enabled);

        Assert.Equal(TimeSpan.FromMilliseconds(2000), delay.RequestedDuration);
    }

    [Fact]
    public async Task InvokeAsync_HeaderAboveSafetyCap_ClampsToMax()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("999999");

        await middleware.InvokeAsync(context, Enabled);

        Assert.Equal(TimeSpan.FromMilliseconds(ChaosOptions.MaxInjectedLatencyMs), delay.RequestedDuration);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-500")]
    [InlineData("0")]
    public async Task InvokeAsync_InvalidOrNonPositiveHeader_TreatedAsAbsent(string headerValue)
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader(headerValue);

        await middleware.InvokeAsync(context, Enabled);

        Assert.Null(delay.RequestedDuration);
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_RegardlessOfInjection()
    {
        var nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new ChaosLatencyInjectionMiddleware(Next, new RecordingChaosDelay());
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Enabled);

        Assert.True(nextCalled);
    }

    private static Task NextThatSucceeds(HttpContext context) => Task.CompletedTask;

    private static HttpContext CreateContextWithHeader(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ChaosLatencyInjectionMiddleware.LatencyHeaderName] = value;
        return context;
    }

    private sealed class RecordingChaosDelay : IChaosDelay
    {
        public TimeSpan? RequestedDuration { get; private set; }

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            RequestedDuration = duration;
            return Task.CompletedTask;
        }
    }
}
