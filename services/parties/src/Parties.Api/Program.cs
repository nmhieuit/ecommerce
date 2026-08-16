using Microsoft.EntityFrameworkCore;
using Parties.Api.Data;
using Parties.Api.Features.Parties;
using Parties.Api.Features.HealthCheck;
using ServiceDefaults;
using Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Registered before the DbContext below, which will be gated on the tenant this resolves.
builder.Services.AddTenancy();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__PartiesDb from the cluster secret store.
builder.Services.AddDbContext<PartiesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PartiesDb")));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();
app.UseTenancy();
app.MapHealthCheckEndpoints();
app.MapPartyEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
