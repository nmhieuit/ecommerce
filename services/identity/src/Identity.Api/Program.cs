using Identity.Api.Features.HealthCheck;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Shell only (tasks.md T001/Setup): this service owns no data and issues no tokens yet. The Duende
// IdentityServer bootstrap, the configuration/user credential stores (data-model.md — Client
// Application, Identity User), and the login flow are User Story 1's job (tasks.md T020-T024).

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();
app.MapHealthCheckEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
