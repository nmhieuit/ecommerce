namespace Tenancy;

/// <summary>
/// The tenant resolved for the current request — a request-scoped holder populated by
/// <see cref="TenantContextMiddleware"/> from the <c>X-Tenant-Id</c> header the gateway stamped.
/// </summary>
/// <remarks>
/// <para>
/// data-model.md gives this exactly two states: Unresolved (<see cref="TenantId"/> null or blank)
/// and Resolved. There is deliberately no third "resolved to a default" state — the whole feature
/// exists to make that state impossible (constitution Principle V).
/// </para>
/// <para>
/// <see cref="TenantId"/> is publicly settable on purpose (research.md Decision 7): test-seeding
/// helpers resolve a <c>DbContext</c> from a manually created DI scope with no HTTP request behind
/// it, so no middleware ever runs for that scope and they must prime the tenant themselves. The
/// guarantee this feature cares about — that a *real request* cannot reach persistence unresolved —
/// is unaffected, because a real request only ever has this set by the middleware.
/// </para>
/// </remarks>
public sealed class TenantContext
{
    /// <summary>
    /// The resolved tenant identifier, or <see langword="null"/> while unresolved.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Returns the resolved tenant identifier, or throws if this request has none.
    /// </summary>
    /// <exception cref="MissingTenantContextException">
    /// The tenant is unresolved. Blank counts as unresolved: a tenant whose identifier is an empty
    /// string is not a tenant, and treating it as one is exactly the fallback Principle V forbids.
    /// </exception>
    public string RequireTenantId() =>
        string.IsNullOrWhiteSpace(TenantId)
            ? throw new MissingTenantContextException()
            : TenantId;
}
