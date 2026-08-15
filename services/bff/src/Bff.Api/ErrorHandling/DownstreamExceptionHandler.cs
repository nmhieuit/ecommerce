using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Polly.Timeout;
using ServiceDefaults;

namespace Bff.Api.ErrorHandling;

/// <summary>
/// Turns a failed downstream call into the structured error the contract promises, instead of an
/// unhandled exception and a generic 500 (research.md Decision 4; spec FR-006).
/// </summary>
/// <remarks>
/// Only <see cref="DownstreamServiceException"/> is handled. Anything else is left alone so a
/// genuine fault inside the BFF still surfaces as a 500 — reporting a bug in this service as
/// "a downstream is unavailable" would send an operator to the wrong team.
/// </remarks>
public sealed partial class DownstreamExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DownstreamExceptionHandler> logger) : IExceptionHandler
{
    private const string UnavailableType = "https://ecommerce.internal/errors/downstream-unavailable";
    private const string TimeoutType = "https://ecommerce.internal/errors/downstream-timeout";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DownstreamServiceException downstream)
        {
            return false;
        }

        var timedOut = IsTimeout(downstream.InnerException);
        var status = timedOut ? HttpStatusCode.GatewayTimeout : HttpStatusCode.BadGateway;
        var correlationId = ResolveCorrelationId(httpContext);

        // The full exception goes to the log, where topology is not a disclosure risk and an
        // operator needs it; only the logical name goes to the caller.
        LogDownstreamFailure(logger, downstream.ServiceName, (int)status, correlationId, downstream);

        httpContext.Response.StatusCode = (int)status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = downstream,
            ProblemDetails = new ProblemDetails
            {
                Type = timedOut ? TimeoutType : UnavailableType,
                Title = timedOut ? "Downstream service timed out" : "Downstream service unavailable",
                Status = (int)status,
                // Names the dependency and nothing about where it lives.
                Detail = timedOut
                    ? $"The '{downstream.ServiceName}' service did not respond within its timeout budget."
                    : $"The '{downstream.ServiceName}' service could not be reached.",
                Extensions = { ["correlationId"] = correlationId },
            },
        });
    }

    /// <summary>
    /// A timeout is reported separately from an unreachable host so a dashboard can distinguish
    /// "the service is gone" from "the service is struggling" — different problems, different fixes.
    /// </summary>
    /// <remarks>
    /// <see cref="TaskCanceledException"/> counts because the resilience pipeline surfaces an
    /// exhausted per-attempt timeout that way rather than as a <see cref="TimeoutRejectedException"/>.
    /// A caller-aborted request never reaches here — the pipeline's own token is what cancels.
    /// </remarks>
    /// <remarks>
    /// A tripped circuit (<see cref="BrokenCircuitException"/>) deliberately falls to the 502
    /// branch rather than 504: the breaker is refusing to call a dependency it already believes is
    /// down, which is unavailability, not slowness.
    /// </remarks>
    private static bool IsTimeout(Exception? inner) =>
        inner is TimeoutRejectedException or TaskCanceledException or OperationCanceledException;

    /// <summary>
    /// Source-generated so the message template is compiled rather than parsed per call
    /// (CA1848). Structured fields only — constitution Principle VII forbids interpolated
    /// log strings, and these are the fields an operator filters on.
    /// </summary>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Downstream call to {DownstreamService} failed with {StatusCode} for correlation {CorrelationId}.")]
    private static partial void LogDownstreamFailure(
        ILogger logger,
        string downstreamService,
        int statusCode,
        string correlationId,
        Exception exception);

    /// <summary>
    /// The ID <c>CorrelationIdMiddleware</c> resolved for this request, so the body and the
    /// <c>X-Correlation-Id</c> response header agree and the error is traceable end to end
    /// (constitution Principle VII).
    /// </summary>
    private static string ResolveCorrelationId(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
        && value is string correlationId
            ? correlationId
            : string.Empty;
}
