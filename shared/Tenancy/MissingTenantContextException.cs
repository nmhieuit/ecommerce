namespace Tenancy;

/// <summary>
/// Thrown when code that requires a resolved tenant runs without one (spec FR-004/FR-005).
/// </summary>
/// <remarks>
/// This exception existing at all is the point: the alternative to throwing is falling back to some
/// default tenant, and constitution Principle V forbids that outright. It is expected to surface
/// only when a request reached a service by some path other than the gateway (spec Edge Cases), so
/// it is a wiring or deployment failure, not a user error to be handled gracefully.
/// </remarks>
public sealed class MissingTenantContextException : Exception
{
    private const string DefaultMessage =
        "No tenant has been resolved for this request. A tenant is resolved once, at the gateway, "
        + "and propagated via the 'X-Tenant-Id' header; there is deliberately no default tenant to "
        + "fall back to (constitution Principle V).";

    public MissingTenantContextException()
        : base(DefaultMessage)
    {
    }

    public MissingTenantContextException(string message)
        : base(message)
    {
    }

    public MissingTenantContextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
