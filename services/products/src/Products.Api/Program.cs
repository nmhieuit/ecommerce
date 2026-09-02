using Identity;
using Microsoft.EntityFrameworkCore;
using Products.Api.Data;
using Products.Api.Features.Catalog;
using Products.Api.Features.HealthCheck;
using ServiceDefaults;
using Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Independent token validation (014-identity-server-auth spec US2/FR-004) — this service does not
// trust that the gateway already authenticated the request; it validates the token itself.
// FallbackPolicy denies by default (research.md Decision 6), so every endpoint requires it unless
// explicitly marked [AllowAnonymous] (the health probes, Features/HealthCheck/HealthCheckEndpoints.cs).
builder.Services.AddIdentityValidation(builder.Configuration);

// Registered before the DbContext below, which will be gated on the tenant this resolves.
builder.Services.AddTenancy();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__ProductsDb from the cluster secret store.
//
// This is the single place a connection to it is ever constructed, and it is gated: RequireTenantId()
// runs before UseSqlServer, so an unresolved request cannot obtain a DbContext at all, let alone
// open a connection with one (constitution Principle V; research.md Decisions 5-6). There is
// deliberately no fallback tenant to resolve to.
builder.Services.AddDbContext<ProductsDbContext>((serviceProvider, options) =>
{
    serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
    options.UseSqlServer(builder.Configuration.GetConnectionString("ProductsDb"));
});

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Authenticate/authorize before tenant resolution — an unauthenticated request is rejected before
// spending any effort resolving a tenant or touching persistence.
app.UseIdentityValidation();

app.UseTenancy();
app.MapHealthCheckEndpoints();
app.MapCatalogEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
