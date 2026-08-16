using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy;

/// <summary>
/// Reads the inbound <c>X-Subject-Id</c> header into the request's <see cref="CallerContext"/> and
/// pushes the subject into the logging scope, so every structured log line for the request carries
/// who made it (constitution Principle VII).
/// </summary>
/// <remarks>
/// A deliberate mirror of <see cref="TenantContextMiddleware"/>, including the decision not to fail
/// here: the gateway is the sole resolution point, so a missing or blank header leaves the context
/// Unresolved rather than triggering any local fallback. The request then proceeds until it reaches
/// a route that requires a caller, which is where <see cref="CallerContext.RequireSubjectId"/> stops
/// it. Failing at the middleware would break the health probes, which a Kubernetes probe calls
/// without going through the gateway and which legitimately have no caller.
/// </remarks>
public sealed class CallerContextMiddleware
{
    public const string HeaderName = "X-Subject-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CallerContextMiddleware> _logger;

    public CallerContextMiddleware(RequestDelegate next, ILogger<CallerContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <param name="callerContext">
    /// Injected per invocation rather than through the constructor: the middleware instance is
    /// effectively a singleton, and the caller context is scoped to one request.
    /// </param>
    public async Task InvokeAsync(HttpContext context, CallerContext callerContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(callerContext);

        if (context.Request.Headers.TryGetValue(HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            var subjectId = header.ToString();
            callerContext.SubjectId = subjectId;

            using (_logger.BeginScope(new Dictionary<string, object> { ["SubjectId"] = subjectId }))
            {
                await _next(context);
            }

            return;
        }

        // Unresolved: no scope is opened, because logging a null or blank SubjectId would read as
        // "this request belongs to the blank caller" rather than "this request has no caller."
        await _next(context);
    }
}
