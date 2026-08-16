using Baskets.Api.Data;
using Baskets.Api.Features.Baskets;
using Baskets.Api.Features.HealthCheck;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults;
using Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Registered before the DbContext below, which will be gated on the tenant this resolves.
builder.Services.AddTenancy();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__BasketsDb from the cluster secret store.
//
// This is the single place a connection to it is ever constructed, and it is gated: RequireTenantId()
// runs before UseSqlServer, so an unresolved request cannot obtain a DbContext at all, let alone
// open a connection with one (constitution Principle V; research.md Decisions 5-6). There is
// deliberately no fallback tenant to resolve to.
builder.Services.AddDbContext<BasketsDbContext>((serviceProvider, options) =>
{
    serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
    options.UseSqlServer(builder.Configuration.GetConnectionString("BasketsDb"));
});

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();
app.UseTenancy();
app.MapHealthCheckEndpoints();
app.MapBasketEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
