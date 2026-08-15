using Bff.Api.ErrorHandling;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// The single place every downstream call is funnelled through, so each one fails the same way.
/// </summary>
/// <remarks>
/// This lives with the typed clients rather than in the route groups. The tasks describe wiring
/// failure handling "into the route groups' downstream-call sites", and doing it here reaches every
/// one of those call sites at once: a route group physically cannot issue a downstream call that
/// bypasses it, whereas a per-route try/catch is something the next route added can forget. It also
/// keeps the route groups pure proxy-and-shape (spec FR-005), with no error-handling branches.
/// </remarks>
internal static class DownstreamCall
{
    /// <summary>
    /// Runs <paramref name="call"/>, translating a resilience-pipeline or transport failure into a
    /// <see cref="DownstreamServiceException"/> that names the dependency.
    /// </summary>
    public static async Task<TResult> ExecuteAsync<TResult>(
        string serviceName,
        Func<Task<TResult>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (IsDownstreamFailure(exception))
        {
            throw new DownstreamServiceException(serviceName, exception);
        }
    }

    /// <summary>
    /// Only failures of the dependency itself are caught. A bug inside the BFF — a null reference
    /// in a mapping function, say — must keep propagating as a 500 rather than being reported as
    /// somebody else's outage.
    /// </summary>
    private static bool IsDownstreamFailure(Exception exception) => exception is
        HttpRequestException          // unreachable host, refused connection, transport failure
        or TimeoutRejectedException   // the resilience pipeline's timeout fired
        or BrokenCircuitException     // the breaker is open and refusing to call
        or TaskCanceledException      // an attempt timeout surfaces this way
        or OperationCanceledException;
}
