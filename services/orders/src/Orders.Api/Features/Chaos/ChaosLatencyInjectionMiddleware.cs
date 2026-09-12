using Microsoft.Extensions.Options;

namespace Orders.Api.Features.Chaos;

/// <summary>
/// The chaos exercise capability, whole: a deliberate, reversible delay for SCRUM-34's
/// "inject latency into orders service" scenario, gated so it can never fire by accident
/// (contracts/chaos-latency-injection-contract.md).
/// </summary>
/// <remarks>
/// Two gates, not one: <see cref="ChaosOptions.AllowLatencyInjection"/> is an environment-level
/// decision (only ever <see langword="true"/> on a diagnostic cluster), and the
/// <see cref="LatencyHeaderName"/> header is the per-request decision of whoever is running the
/// exercise right now. Splitting them means starting or stopping the injection mid-exercise is just
/// sending or not sending a header — no redeploy, no restart (research.md Quyết định 1; constitution
/// Principle X).
/// </remarks>
public sealed class ChaosLatencyInjectionMiddleware
{
    public const string LatencyHeaderName = "X-Chaos-Latency-Ms";

    private readonly RequestDelegate _next;
    private readonly IChaosDelay _delay;

    public ChaosLatencyInjectionMiddleware(RequestDelegate next, IChaosDelay delay)
    {
        _next = next;
        _delay = delay;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<ChaosOptions> options)
    {
        if (options.Value.AllowLatencyInjection && TryGetRequestedDelay(context, out var duration))
        {
            await _delay.DelayAsync(duration, context.RequestAborted);
        }

        // Always runs, with the same request/response untouched either way — this middleware only
        // ever changes *when* the rest of the pipeline runs, never what it returns
        // (contracts/chaos-latency-injection-contract.md Bất biến 6).
        await _next(context);
    }

    /// <summary>
    /// Missing, unparsable, or non-positive header values are all treated as "no delay requested" —
    /// none of them are an operator error worth failing the request over
    /// (contracts/chaos-latency-injection-contract.md Bất biến 2-3). A value above
    /// <see cref="ChaosOptions.MaxInjectedLatencyMs"/> is honoured only up to that cap (Bất biến 4).
    /// </summary>
    private static bool TryGetRequestedDelay(HttpContext context, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        if (!context.Request.Headers.TryGetValue(LatencyHeaderName, out var headerValues))
        {
            return false;
        }

        if (!int.TryParse(headerValues.ToString(), out var requestedMs) || requestedMs <= 0)
        {
            return false;
        }

        var clampedMs = Math.Min(requestedMs, ChaosOptions.MaxInjectedLatencyMs);
        duration = TimeSpan.FromMilliseconds(clampedMs);
        return true;
    }
}
