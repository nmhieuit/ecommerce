namespace Tenancy;

/// <summary>
/// Thrown when code that requires a resolved caller runs without one.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="MissingTenantContextException"/>, and it exists for the same
/// reason: the alternative to throwing is picking some caller, and a picked caller means one
/// shopper reading or checking out another shopper's basket. Expected to surface only when a
/// request reached a service by some path other than the gateway, so it is a wiring or deployment
/// failure rather than a user error to be handled gracefully.
/// </remarks>
public sealed class MissingCallerContextException : Exception
{
    private const string DefaultMessage =
        "No caller has been resolved for this request. A caller is resolved once, at the gateway, "
        + "and propagated via the 'X-Subject-Id' header; there is deliberately no default caller to "
        + "fall back to.";

    public MissingCallerContextException()
        : base(DefaultMessage)
    {
    }

    public MissingCallerContextException(string message)
        : base(message)
    {
    }

    public MissingCallerContextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
