using Tenancy;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Copies the caller identity the BFF received — the tenant and, since
/// 004-minimal-shopping-spa, the subject — onto every outbound downstream call
/// (contracts/tenant-id-header.md and 004's contracts/subject-id-header.md: the BFF relays, it
/// never resolves).
/// </summary>
/// <remarks>
/// <para>
/// The type name predates the subject header and is kept so that the two propagation contracts,
/// the plan, and the tasks that reference it still point at the right file. It relays both headers.
/// </para>
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

        var requestServices = httpContextAccessor.HttpContext?.RequestServices;

        Relay(
            request,
            TenantContextMiddleware.HeaderName,
            requestServices?.GetService<TenantContext>()?.TenantId);

        Relay(
            request,
            CallerContextMiddleware.HeaderName,
            requestServices?.GetService<CallerContext>()?.SubjectId);

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Removes then conditionally re-adds, rather than setting: the removal is what stops a stale
    /// value surviving on a retried message, and it is what makes "unresolved" travel as the
    /// absence of a header rather than as some previous request's value.
    /// </summary>
    /// <remarks>
    /// Unresolved stays unresolved — no header is sent, the downstream service observes nothing,
    /// and its own gate refuses. The failure propagates rather than being masked.
    /// </remarks>
    private static void Relay(HttpRequestMessage request, string headerName, string? value)
    {
        // Never merge with whatever the message already carries: the inbound request is the only
        // authority.
        request.Headers.Remove(headerName);

        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Headers.Add(headerName, value);
        }
    }
}
