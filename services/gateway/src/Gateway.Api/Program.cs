using Gateway.Api.Features.HealthCheck;
using Gateway.Api.Identity;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Constitution Principle V: the tenant is resolved here, at the edge, and nowhere else.
// 014-identity-server-auth: which scheme actually authenticates a given request — the Phase 1
// stub, or the real identity server's JwtBearer — is decided per request from a live-reloaded
// toggle, not fixed at startup (research.md Decision 2/7). Everything downstream of the resolved
// claims (TenantHeaderPropagationMiddleware, SubjectHeaderPropagationMiddleware, and everything
// they feed) is unchanged either way — spec FR-008.
builder.Services.AddToggleGatedIdentity(builder.Configuration);

// No AddDbContext here, unlike the domain services: the gateway owns no data and reads no
// service's database (constitution Principle I; plan.md Technical Context — Storage: N/A).

// The storefront is served from its own origin and calls the gateway cross-origin, so without a
// policy here the browser blocks every request before it leaves — which no curl check and no
// mocked-fetch component test can reveal. Origins come from configuration rather than a literal:
// a deployment that serves the storefront from the gateway's own origin needs none of this, and one
// that does not must state its origin explicitly.
//
// Explicit origins, not a wildcard: the client sends credentials, and the CORS specification
// forbids `*` on a credentialed request.
//
// WithExposedHeaders(X-Correlation-Id): without it, the browser still shows the header in
// DevTools' Network tab (that view is never subject to CORS), but the SPA's own JS reading
// response.headers.get(...) would get null — X-Correlation-Id is not one of the safelisted
// response headers a browser exposes to script by default (016-correlation-id-propagation
// research.md Decision 5; contracts/spa-correlation-visibility-contract.md).
builder.Services.AddCors(options => options.AddPolicy(
    StorefrontCorsPolicy,
    policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

// The gateway's whole routing surface, loaded from the ReverseProxy section rather than defined
// in code (research.md Decision 2), so the route table stays reviewable as data and swappable per
// environment without a rebuild. Exactly one cluster is configured, the BFF (Decision 1): the
// gateway never routes to a domain service directly.
//
// 020-timeouts-retry-circuit-breaker (research.md Decision 3): the circuit-breaker half of
// Principle VIII for this hop — bff-cluster's appsettings.json HealthCheck:Passive section selects
// YARP's built-in TransportFailureRateHealthPolicy by name (no separate registration call needed;
// it ships as one of the default IPassiveHealthCheckPolicy implementations). Its options bind from
// configuration too, so a test can override MinimalTotalCountThreshold without touching this file
// (PassiveHealthCheckCircuitBreakerTests) while production keeps the framework defaults (nothing
// under "ReverseProxy:TransportFailureRateHealthPolicy" is set).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.Configure<Yarp.ReverseProxy.Health.TransportFailureRateHealthPolicyOptions>(
    builder.Configuration.GetSection("ReverseProxy:TransportFailureRateHealthPolicy"));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Before authentication and before the proxy: a preflight OPTIONS carries no credentials and must
// be answered by the gateway itself rather than forwarded downstream.
app.UseCors(StorefrontCorsPolicy);

// Authenticate, then authorize (deny-by-default FallbackPolicy — AddToggleGatedIdentity), then turn
// the resolved identity's tenant and subject claims into the headers every hop below reads. All
// four must run before MapReverseProxy: once YARP forwards the request, the headers it copied are
// already decided, and a request that failed authorization never reaches this far regardless.
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantHeaderPropagationMiddleware>();
app.UseMiddleware<SubjectHeaderPropagationMiddleware>();

// Mapped before the proxy so the gateway answers its own probes. Routing prefers the more specific
// /health/* over the proxy's {**catch-all} regardless of order, but relying on that silently would
// mean a BFF outage could restart every gateway pod if the route table ever changed.
app.MapHealthCheckEndpoints();

// Everything else is forwarded to the BFF. A catch-all is deliberate: a gateway that enumerated
// the BFF's paths would need editing every time the BFF gained one, which is the topology coupling
// this feature exists to remove (spec FR-001).
//
// 020-timeouts-retry-circuit-breaker (research.md Decision 3): the parameterless MapReverseProxy()
// used before this feature does not include UsePassiveHealthChecks() — appsettings.json's
// HealthCheck:Passive section configures the policy, but without this middleware in the pipeline
// nothing ever evaluates it or excludes a destination it marks unhealthy, so the circuit never
// opens. UseLoadBalancing() is the middleware that actually filters proxied requests down to
// available (non-unhealthy) destinations, so it must run too, even with a single destination.
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseLoadBalancing();
});

app.Run();

public partial class Program
{
    /// <summary>
    /// The named CORS policy the storefront is admitted by (004-minimal-shopping-spa spec FR-014 —
    /// the storefront reaches the gateway and nothing else, so this is the only origin policy the
    /// platform needs).
    /// </summary>
    public const string StorefrontCorsPolicy = "storefront";
}
