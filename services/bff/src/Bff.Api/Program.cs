using Bff.Api.DownstreamClients;
using Bff.Api.Features.Baskets;
using Bff.Api.Features.HealthCheck;
using Bff.Api.Features.Orders;
using Bff.Api.Features.Parties;
using Bff.Api.Features.Products;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// No AddDbContext here, unlike the domain services: the BFF owns no data and reads no service's
// database (constitution Principle I; plan.md Technical Context — Storage: N/A). It reaches the
// four domain services over HTTP only, through the typed clients registered below — each with an
// explicit timeout and resilience pipeline, so no unbounded wait exists (Principle VIII).
builder.Services.AddDownstreamClients(builder.Configuration);

// Native OpenAPI document generation (research.md Decision 6 / ADR-0003), deliberately not
// Swashbuckle. The document this publishes at /openapi/v1.json is the contract the frontend's
// Orval codegen consumes (ADR-0004) and the contract-first source of truth for the SPA in
// SCRUM-14.
builder.Services.AddOpenApi();

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Exposed in Development only. The document describes the BFF's whole client-facing surface, and
// there is no authorization in front of it yet (plan.md Complexity Tracking, Principle VI
// deviation) — publishing it from a deployed environment would hand out that map anonymously.
// The codegen pipeline reads it from a local/CI run, which is a Development host.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthCheckEndpoints();

// The client-facing surface: one route group per capability, each a proxy-and-shape over exactly
// one downstream client and nothing more (spec FR-005).
app.MapProductsEndpoints();
app.MapBasketsEndpoints();
app.MapOrdersEndpoints();
app.MapPartiesEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
