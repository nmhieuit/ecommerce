using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy;

/// <summary>
/// Reads the inbound <c>X-Tenant-Id</c> header into the request's <see cref="TenantContext"/> and
/// pushes the tenant into the logging scope, so every structured log line for the request carries
/// it (constitution Principle VII — the same mechanism <c>CorrelationIdMiddleware</c> uses).
/// </summary>
/// <remarks>
/// This middleware only ever <em>reads</em>. The gateway is the sole resolution point (constitution
/// Principle V), so a missing or blank header leaves the context Unresolved rather than triggering
/// any local fallback — the request is then free to proceed until it tries to touch persistence,
/// which is where <see cref="TenantContext.RequireTenantId"/> stops it. Failing here instead would
/// break endpoints that legitimately need no tenant at all, such as the health checks a probe calls
/// without going through the gateway.
/// </remarks>
public sealed class TenantContextMiddleware
{
    public const string HeaderName = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <param name="tenantContext">
    /// Injected per invocation rather than through the constructor: the middleware instance is
    /// effectively a singleton, and the tenant context is scoped to one request.
    /// </param>
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (context.Request.Headers.TryGetValue(HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            var tenantId = header.ToString();
            tenantContext.TenantId = tenantId;

            using (_logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
            {
                await _next(context);
            }

            return;
        }

        // Unresolved: no scope is opened, because logging a null or blank TenantId would read as
        // "this request belongs to the blank tenant" rather than "this request has no tenant."
        await _next(context);
    }
}
