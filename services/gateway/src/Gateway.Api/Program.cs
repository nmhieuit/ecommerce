using Gateway.Api.Features.HealthCheck;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// No AddDbContext here, unlike the domain services: the gateway owns no data and reads no
// service's database (constitution Principle I; plan.md Technical Context — Storage: N/A).

// The gateway's whole routing surface, loaded from the ReverseProxy section rather than defined
// in code (research.md Decision 2), so the route table stays reviewable as data and swappable per
// environment without a rebuild. Exactly one cluster is configured, the BFF (Decision 1): the
// gateway never routes to a domain service directly.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Mapped before the proxy so the gateway answers its own probes. Routing prefers the more specific
// /health/* over the proxy's {**catch-all} regardless of order, but relying on that silently would
// mean a BFF outage could restart every gateway pod if the route table ever changed.
app.MapHealthCheckEndpoints();

// Everything else is forwarded to the BFF. A catch-all is deliberate: a gateway that enumerated
// the BFF's paths would need editing every time the BFF gained one, which is the topology coupling
// this feature exists to remove (spec FR-001).
app.MapReverseProxy();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
