using Bff.Api.Features.HealthCheck;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// No AddDbContext here, unlike the domain services: the BFF owns no data and reads no service's
// database (constitution Principle I; plan.md Technical Context — Storage: N/A). It reaches the
// four domain services over HTTP through the typed clients T018-T021 add.

// Native OpenAPI document generation (research.md Decision 6 / ADR-0003), deliberately not
// Swashbuckle. The document this publishes at /openapi/v1.json is the contract the frontend's
// Orval codegen consumes (ADR-0004) and the contract-first source of truth for the SPA in
// SCRUM-14 — so it is wired up now, before the routes it will describe exist.
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

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
