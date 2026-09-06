using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ServiceDefaults;

/// <summary>
/// Resolves the correlation ID for a request: reuses an inbound <c>X-Correlation-Id</c> header
/// if present (constitution Principle VII requires it to be generated once "at the edge" and
/// propagated from there), otherwise generates one. The ID is echoed back on the response,
/// attached to the current <see cref="Activity"/> so it flows into every OTel trace/span, and
/// pushed into the logging scope so every structured log line for this request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Written back onto the *request* so anything that forwards this request carries the ID
        // onward — the gateway's reverse proxy copies inbound request headers, so without this a
        // generated ID would reach only this service's own response and the next hop would mint a
        // second, unrelated one. Principle VII requires the ID generated at the edge to propagate
        // across every synchronous call, not merely to be reported by the edge.
        context.Request.Headers[HeaderName] = correlationId;

        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag("correlation.id", correlationId);

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    /// <summary>Longer than any real caller needs (a GUID with dashes is 36), short enough to bound log growth from a single misbehaving/malicious value.</summary>
    private const int MaxLength = 128;

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing) &&
            IsValidCorrelationId(existing.ToString()))
        {
            return existing.ToString();
        }

        return Guid.NewGuid().ToString("n");
    }

    /// <summary>
    /// Deliberately not a GUID-format check: a caller may supply any opaque token (research.md
    /// Decision 2 of 016-correlation-id-propagation), so this only excludes what would actually be
    /// harmful once the value is pushed unfiltered into every structured log line for the request —
    /// a control character (which could forge a second, fake log line — CRLF injection) or an
    /// unbounded length (which could bloat every log entry the request touches).
    /// </summary>
    private static bool IsValidCorrelationId(string value)
    {
        if (value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c < '\x20' || c == '\x7f')
            {
                return false;
            }
        }

        return true;
    }
}
