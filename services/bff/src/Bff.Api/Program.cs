using Bff.Api.DownstreamClients;
using Bff.Api.ErrorHandling;
using Bff.Api.Features.Baskets;
using Bff.Api.Features.Checkout;
using Bff.Api.Features.HealthCheck;
using Bff.Api.Features.Orders;
using Bff.Api.Features.Parties;
using Bff.Api.Features.Products;
using Identity;
using ServiceDefaults;
using Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Independent token validation (014-identity-server-auth spec US2/FR-004) — the BFF does not trust
// that the gateway already authenticated the request; it validates the token itself. FallbackPolicy
// denies by default (research.md Decision 6), so every endpoint requires it unless explicitly
// marked [AllowAnonymous] (the health probes, Features/HealthCheck/HealthCheckEndpoints.cs).
builder.Services.AddIdentityValidation(builder.Configuration);

// The BFF resolves nothing: it reads the tenant the gateway already resolved off the inbound
// X-Tenant-Id header (constitution Principle V — one resolution point, at the edge) and relays it
// onto every downstream call.
builder.Services.AddTenancy();

// No AddDbContext here, unlike the domain services: the BFF owns no data and reads no service's
// database (constitution Principle I; plan.md Technical Context — Storage: N/A). It reaches the
// four domain services over HTTP only, through the typed clients registered below — each with an
// explicit timeout and resilience pipeline, so no unbounded wait exists (Principle VIII).
builder.Services.AddDownstreamClients(builder.Configuration);

// A downstream outage must reach the caller as a structured, bounded error rather than an
// unhandled exception (spec FR-006; research.md Decision 4). Registered before the routes so no
// route can be added that is not covered by it.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DownstreamExceptionHandler>();

// Native OpenAPI document generation (research.md Decision 6 / ADR-0003), deliberately not
// Swashbuckle. The document this publishes at /openapi/v1.json is the contract the frontend's
// Orval codegen consumes (ADR-0004) and the contract-first source of truth for the SPA in
// SCRUM-14.
builder.Services.AddOpenApi();

builder.Services.AddHealthCheckFeature();

var app = builder.Build();

// Outermost of the app's own middleware, so it catches from everything after it. It sits inside
// UseServiceDefaults' correlation-ID middleware deliberately: the handler reads the correlation ID
// that middleware resolves, so that must run first.
app.UseServiceDefaults();

// Authenticate/authorize before tenant resolution — an unauthenticated request is rejected before
// spending any effort resolving a tenant or calling downstream.
app.UseIdentityValidation();

// Outside the exception handler so that the handler's own log lines carry the tenant too, the same
// way UseServiceDefaults' correlation ID wraps everything after it.
app.UseTenancy();

app.UseExceptionHandler();

// Exposed in Development only, and explicitly AllowAnonymous rather than inheriting the
// FallbackPolicy above (014-identity-server-auth): the codegen pipeline (ADR-0004) fetches this at
// build time, not as a logged-in user, so there is no bearer token to send. Restricting exposure to
// Development — never a deployed environment — is what keeps this from handing out the BFF's whole
// client-facing surface map anonymously in production; it is not a substitute for that restriction.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthCheckEndpoints();

// The client-facing surface: one route group per capability, each a proxy-and-shape over exactly
// one downstream client and nothing more (spec FR-005).
app.MapProductsEndpoints();
app.MapBasketsEndpoints();
app.MapOrdersEndpoints();
app.MapPartiesEndpoints();

// The one route that spans a workflow rather than a single read (004 research.md Decision 9).
app.MapCheckoutEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
