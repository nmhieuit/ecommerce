using Tenancy;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Copies the tenant the BFF received onto every outbound downstream call
/// (contracts/tenant-id-header.md — the BFF relays, it never resolves).
/// </summary>
/// <remarks>
/// <para>
/// This exists because a typed <see cref="HttpClient"/> forwards nothing by itself. YARP copies
/// inbound headers on the gateway → BFF hop for free, which makes it easy to assume the whole chain
/// works; without this handler the BFF → domain-service hop would silently drop the tenant
/// (research.md Decision 4).
/// </para>
/// <para>
/// The tenant is read through <see cref="IHttpContextAccessor"/> rather than by injecting
/// <see cref="TenantContext"/> directly, and that is not incidental. <c>IHttpClientFactory</c>
/// builds message handlers in their own scope and reuses them across requests for the handler's
/// lifetime, so a scoped service injected here would be captured from whichever request happened to
/// create the handler — and every later request would then be stamped with that request's tenant.
/// For a tenant identifier, that failure mode is cross-tenant data access, so it is worth the
/// indirection.
/// </para>
/// </remarks>
public sealed class TenantPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Never merge with whatever the message already carries: the inbound request's tenant is
        // the only authority, and a stale value on a retried message must not survive.
        request.Headers.Remove(TenantContextMiddleware.HeaderName);

        var tenantId = httpContextAccessor.HttpContext
            ?.RequestServices.GetService<TenantContext>()
            ?.TenantId;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.Add(TenantContextMiddleware.HeaderName, tenantId);
        }

        // Unresolved stays unresolved: no header is sent, the downstream service observes no tenant,
        // and its own gate refuses persistence. The failure propagates rather than being masked.
        return base.SendAsync(request, cancellationToken);
    }
}
