using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Identity;

/// <summary>
/// Wraps the JwtBearer backchannel — the HttpClient every service uses to fetch the identity
/// server's OIDC discovery document and JWKS — with an explicit timeout, retry, and circuit breaker,
/// instead of leaving <see cref="JwtBearerOptions.Backchannel"/> at the framework's own default
/// <see cref="System.Net.Http.HttpClient"/> (020-timeouts-retry-circuit-breaker spec FR-001;
/// research.md Decision 4).
/// </summary>
/// <remarks>
/// Called once from <see cref="IdentityValidationExtensions.AddIdentityValidation"/> (covers the
/// BFF and all four domain services) and once from the gateway's own
/// <c>ToggleGatedAuthenticationExtensions.AddToggleGatedIdentity</c> (which cannot call
/// <c>AddIdentityValidation</c> directly — see that class's remarks). Both call sites share this one
/// method so the backchannel's timeout/retry/circuit-breaker tuning lives in exactly one place,
/// rather than six copies that could quietly drift apart.
/// </remarks>
public static class IdentityBackchannelResilience
{
    /// <summary>
    /// The named <c>HttpClient</c> every JwtBearer scheme's <c>Backchannel</c> resolves to. Named
    /// (not typed) because <see cref="JwtBearerOptions"/> is framework-owned — there is no client
    /// type of ours to register against.
    /// </summary>
    public const string BackchannelClientName = "IdentityBackchannel";

    /// <summary>
    /// The identity server's OIDC discovery document and JWKS are cached by the framework's
    /// <c>ConfigurationManager</c> and re-fetched only occasionally (on cache expiry, or on a
    /// signing-key validation failure) — nowhere near the per-request volume the four downstream
    /// business clients see. A more generous attempt budget than
    /// <c>DownstreamClientRegistrationExtensions.AttemptTimeout</c> (1 s) is appropriate: a slow
    /// identity server should not fail token validation for every in-flight request just because
    /// this rare background fetch was tight on time.
    /// </summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Must be ≥ <see cref="AttemptTimeout"/>; leaves room for the pipeline's default retry.</summary>
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Registers the resilience-wrapped backchannel client and points the named JwtBearer scheme's
    /// <see cref="JwtBearerOptions.Backchannel"/> at it. Retry and circuit-breaker behaviour beyond
    /// the two timeouts above are left at <c>AddStandardResilienceHandler</c>'s own defaults — this
    /// call site has no request-per-second volume to tune a custom threshold against, unlike the
    /// BFF's downstream clients (research.md Decision 2).
    /// </summary>
    public static IServiceCollection AddIdentityBackchannelResilience(
        this IServiceCollection services,
        string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        services.AddHttpClient(BackchannelClientName)
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = TotalRequestTimeout;
            });

        services.AddOptions<JwtBearerOptions>(authenticationScheme)
            .Configure<IHttpClientFactory>((jwtOptions, httpClientFactory) =>
            {
                jwtOptions.Backchannel = httpClientFactory.CreateClient(BackchannelClientName);
            });

        return services;
    }
}
