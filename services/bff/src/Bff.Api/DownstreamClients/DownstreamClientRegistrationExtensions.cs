namespace Bff.Api.DownstreamClients;

/// <summary>
/// Registers the BFF's four downstream clients identically, so no one of them can quietly end up
/// without a timeout or a resilience pipeline.
/// </summary>
public static class DownstreamClientRegistrationExtensions
{
    /// <summary>
    /// Constitution Principle VIII: no unbounded wait may exist. These budgets sit above the
    /// internal-service-API SLO the four downstream services declare (p95 150 ms / p99 500 ms) and
    /// below spec SC-003's ceiling — a caller must get a clear error in under 5 seconds, so the
    /// total time one BFF request may spend on one downstream, retries included, is capped at 3 s.
    /// </summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Changing this requires checking the gateway's forwarding timeout, which must stay ≥ it
    /// (data-model.md, Route Mapping). That relationship is enforced by
    /// <c>Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests</c>, which mirrors this value.
    /// </summary>
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The standard handler's default retry backoff starts at 2 s, which does not fit inside a 3 s
    /// total budget: the first retry's delay alone would exhaust it, so a downstream that refuses a
    /// connection instantly would still be reported as a timeout (504) rather than as unavailable
    /// (502), collapsing a distinction the error contract draws deliberately. At 200 ms all three
    /// attempts complete well inside the budget, so a fast failure stays a fast failure and only a
    /// genuinely slow downstream produces a timeout.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    private const int MaxRetryAttempts = 2;

    /// <summary>
    /// Adds all four typed clients with validated configuration and a standard resilience
    /// pipeline (timeout + retry + circuit breaker) — research.md Decision 3.
    /// </summary>
    public static IServiceCollection AddDownstreamClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDownstreamClient<ProductsApiClient>(ProductsApiClient.ServiceName, configuration);
        services.AddDownstreamClient<BasketsApiClient>(BasketsApiClient.ServiceName, configuration);
        services.AddDownstreamClient<OrdersApiClient>(OrdersApiClient.ServiceName, configuration);
        services.AddDownstreamClient<PartiesApiClient>(PartiesApiClient.ServiceName, configuration);

        return services;
    }

    private static void AddDownstreamClient<TClient>(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
        where TClient : class
    {
        services.AddDownstreamServiceOptions(serviceName, configuration);

        // The HttpClient is registered under the service's logical name, so its handler pipeline
        // can be reconfigured by name — which is how integration tests point it at an in-process
        // downstream without the BFF knowing it is under test (research.md Decision 5).
        services.AddHttpClient<TClient>(serviceName, (provider, client) =>
            {
                // Resolving the options here rather than at registration time means validation
                // failures surface through the same ValidateOnStart path as every other option,
                // instead of during service-collection construction.
                client.BaseAddress = provider.GetDownstreamOptions(serviceName).BaseUrl;
            })
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = TotalRequestTimeout;

                resilience.Retry.MaxRetryAttempts = MaxRetryAttempts;
                resilience.Retry.Delay = RetryDelay;

                // The breaker's sampling window must span at least two attempt timeouts, or it
                // could trip on a single slow call rather than on a genuine failure rate.
                resilience.CircuitBreaker.SamplingDuration = CircuitBreakerSamplingDuration;
            });
    }
}
