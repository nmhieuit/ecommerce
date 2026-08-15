using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Features.Orders;
using Orders.Api.Features.HealthCheck;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__OrdersDb from the cluster secret store.
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb")));

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();
app.MapHealthCheckEndpoints();
app.MapOrderEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
