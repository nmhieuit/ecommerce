using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ServiceDefaults;

/// <summary>
/// Deliberate, reversible 5xx responses, so SCRUM-35's "simulate enough synthetic errors to exhaust a
/// service's monthly error budget" scenario burns the budget through the real path — ASP.NET Core
/// instrumentation records the 500 on the server span, OTel exports it, the error-budget rules count
/// it (contracts/chaos-fault-injection-contract.md).
/// </summary>
/// <remarks>
/// Two gates, the same split as 025's ChaosLatencyInjectionMiddleware:
/// <see cref="ChaosFaultOptions.AllowFaultInjection"/> is the environment-level decision, and the
/// <see cref="FaultHeaderName"/> header is the per-request decision of whoever runs the exercise.
/// Only the exact value <see cref="FaultHeaderValue"/> counts, so a mistyped header never quietly
/// turns into a real failure.
/// </remarks>
public sealed class ChaosFaultInjectionMiddleware
{
    public const string FaultHeaderName = "X-Chaos-Fault";
    public const string FaultHeaderValue = "5xx";

    private readonly RequestDelegate _next;

    public ChaosFaultInjectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, IOptions<ChaosFaultOptions> options)
    {
        if (options.Value.AllowFaultInjection
            && context.Request.Headers.TryGetValue(FaultHeaderName, out var headerValues)
            && string.Equals(headerValues.ToString(), FaultHeaderValue, StringComparison.Ordinal))
        {
            // Short-circuits: nothing past this point runs, so an injected fault never touches business
            // data (contract Bất biến 3).
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }

        // Untouched otherwise — this middleware only ever replaces a response when told to
        // (contract Bất biến 6).
        return _next(context);
    }
}
