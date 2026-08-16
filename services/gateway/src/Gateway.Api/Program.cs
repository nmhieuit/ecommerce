using Gateway.Api.Features.HealthCheck;
using Gateway.Api.Identity;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Constitution Principle V: the tenant is resolved here, at the edge, and nowhere else. Phase 1
// resolves it from a fixed stub identity; Phase 3 (SCRUM-23) swaps this one registration for
// AddJwtBearer(...) against the real identity server and nothing downstream changes (spec FR-007).
builder.Services.AddAuthentication(StubIdentityAuthenticationHandler.SchemeName)
    .AddScheme<StubIdentityAuthenticationSchemeOptions, StubIdentityAuthenticationHandler>(
        StubIdentityAuthenticationHandler.SchemeName,
        options => builder.Configuration.GetSection("StubIdentity").Bind(options));

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
builder.Services.AddCors(options => options.AddPolicy(
    StorefrontCorsPolicy,
    policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// The gateway's whole routing surface, loaded from the ReverseProxy section rather than defined
// in code (research.md Decision 2), so the route table stays reviewable as data and swappable per
// environment without a rebuild. Exactly one cluster is configured, the BFF (Decision 1): the
// gateway never routes to a domain service directly.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Before authentication and before the proxy: a preflight OPTIONS carries no credentials and must
// be answered by the gateway itself rather than forwarded downstream.
app.UseCors(StorefrontCorsPolicy);

// Authenticate first, then turn the resolved identity's tenant and subject claims into the headers
// every hop below reads. All three must run before MapReverseProxy: once YARP forwards the request,
// the headers it copied are already decided.
app.UseAuthentication();
app.UseMiddleware<TenantHeaderPropagationMiddleware>();
app.UseMiddleware<SubjectHeaderPropagationMiddleware>();

// Mapped before the proxy so the gateway answers its own probes. Routing prefers the more specific
// /health/* over the proxy's {**catch-all} regardless of order, but relying on that silently would
// mean a BFF outage could restart every gateway pod if the route table ever changed.
app.MapHealthCheckEndpoints();

// Everything else is forwarded to the BFF. A catch-all is deliberate: a gateway that enumerated
// the BFF's paths would need editing every time the BFF gained one, which is the topology coupling
// this feature exists to remove (spec FR-001).
app.MapReverseProxy();

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
