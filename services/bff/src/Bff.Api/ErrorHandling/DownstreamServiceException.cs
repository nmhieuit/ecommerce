namespace Bff.Api.ErrorHandling;

/// <summary>
/// A downstream service failed to answer. Carries the failing dependency's <em>logical</em> name so
/// the error can be attributed without the handler having to guess which call site threw.
/// </summary>
/// <remarks>
/// Deliberately carries the logical name and nothing else — no base address, no request URI. What
/// this type does not hold cannot be leaked to a client by a later change to the error handler
/// (data-model.md Error response validation rule; spec FR-001).
/// </remarks>
public sealed class DownstreamServiceException(string serviceName, Exception innerException)
    : Exception($"The '{serviceName}' service did not answer.", innerException)
{
    /// <summary>The failing dependency's logical name, e.g. <c>ProductsApi</c>.</summary>
    public string ServiceName { get; } = serviceName;
}
