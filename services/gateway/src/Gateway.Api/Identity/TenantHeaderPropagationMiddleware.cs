namespace Gateway.Api.Identity;

/// <summary>
/// Turns the authenticated identity's tenant claim into the <c>X-Tenant-Id</c> request header that
/// every hop below the gateway reads (contracts/tenant-id-header.md).
/// </summary>
/// <remarks>
/// <para>
/// Written onto the <em>request</em>, exactly as <c>CorrelationIdMiddleware</c> does and for the
/// same reason: YARP forwards inbound request headers, so this is what makes the value travel
/// (research.md Decision 2).
/// </para>
/// <para>
/// Any inbound value is always overwritten, never merged or trusted. Constitution Principle V makes
/// the gateway the sole resolution point, and a header the caller controls is precisely the
/// "inferred from user-supplied request data" case it forbids.
/// </para>
/// </remarks>
public sealed class TenantHeaderPropagationMiddleware
{
    public const string HeaderName = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantHeaderPropagationMiddleware> _logger;

    public TenantHeaderPropagationMiddleware(
        RequestDelegate next,
        ILogger<TenantHeaderPropagationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType)?.Value;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Nothing resolved a tenant for this request, so nothing may claim one on its behalf.
            // Removing rather than leaving the inbound value alone matters: passing a caller's own
            // header through unchecked is exactly the smuggling route the overwrite above closes.
            context.Request.Headers.Remove(HeaderName);

            await _next(context);
            return;
        }

        context.Request.Headers[HeaderName] = tenantId;

        // The same logging scope Tenancy's TenantContextMiddleware opens at every hop below. The
        // gateway does not use that middleware (research.md Decision 3 — it produces the header
        // rather than consuming one), but constitution Principle VII wants the tenant on every
        // hop's logs, and the hop that resolved it is the last one that should be missing from
        // that trail (quickstart.md Scenario 1).
        using (_logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
        {
            await _next(context);
        }
    }
}
