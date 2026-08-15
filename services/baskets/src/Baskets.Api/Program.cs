using Baskets.Api.Data;
using Baskets.Api.Features.Baskets;
using Baskets.Api.Features.HealthCheck;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__BasketsDb from the cluster secret store.
builder.Services.AddDbContext<BasketsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BasketsDb")));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();
app.MapHealthCheckEndpoints();
app.MapBasketEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
