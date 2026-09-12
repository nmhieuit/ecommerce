using Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Features.Orders;
using Orders.Api.Features.HealthCheck;
using ServiceDefaults;
using Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// specs/018-cluster-secret-store FR-007: fail fast at startup if the cluster never injected this
// service's database credential, instead of starting and only discovering it on the first request.
builder.AddRequiredSecretsValidation(RequiredSecret.ConnectionString("OrdersDb"));

// Independent token validation (014-identity-server-auth spec US2/FR-004) — this service does not
// trust that the gateway already authenticated the request; it validates the token itself.
// FallbackPolicy denies by default (research.md Decision 6), so every endpoint requires it unless
// explicitly marked [AllowAnonymous] (the health probes, Features/HealthCheck/HealthCheckEndpoints.cs).
builder.Services.AddIdentityValidation(builder.Configuration);

// Registered before the DbContext below, which will be gated on the tenant this resolves.
builder.Services.AddTenancy();

// The one and only database this service is given a route to. The key is service-scoped so no
// service can pick up another's connection by accident (spec FR-005); the value is overridden
// per environment via ConnectionStrings__OrdersDb from the cluster secret store.
//
// This is the single place a connection to it is ever constructed. Construction itself is NOT
// gated on a resolved tenant (024-verify-transactional-outbox research.md Decision 6/Program.cs
// note below) — the gate moved to each call site that actually touches Order rows
// (OrderEndpoints.cs calls tenant.RequireTenantId() explicitly), because MassTransit's outbox
// delivery/cleanup hosted services (Program.cs's AddEntityFrameworkOutbox<OrdersDbContext> below)
// construct this same context from their own background scope, with no HTTP request — and
// therefore no resolved tenant — behind it. Per MassTransit's own maintainers (GitHub discussion
// #5893), gating DbContext construction itself on a scoped, request-only dependency is
// unsupported. The outbox/inbox tables are platform messaging infrastructure, not tenant-owned
// business data, so this is a correction to where Principle V's guarantee actually applies, not a
// weakening of it — every code path that reads or writes an Order row still requires a resolved
// tenant, just asserted at that call site instead of at DbContext construction.
builder.Services.AddDbContext<OrdersDbContext>((_, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb"));
});

builder.Services.AddHealthCheckFeature();

// 024-verify-transactional-outbox (research.md Decision 2): Bus Outbox writes the outbox record in
// the same EF Core transaction as OrdersDbContext.SaveChangesAsync(), and its hosted delivery
// service polls the table and publishes to RabbitMQ — including on the first poll after a fresh
// process start, which is what makes a pending message survive a crash between commit and publish
// (spec FR-003/FR-004). QueryDelay is configurable (Outbox:QueryDelaySeconds) so an integration
// test can widen it past its own test duration to deterministically catch "committed but not yet
// sent" before simulating a crash (tasks.md T016).
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrdersDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(builder.Configuration.GetValue("Outbox:QueryDelaySeconds", 1));
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        // ConnectionStrings:RabbitMq (a full amqp:// URI, e.g. from a Testcontainers instance in
        // integration tests) takes precedence over the discrete RabbitMq:Host/Username/Password
        // (docker-compose.yml/.deps.yml) — same "full connection string wins" precedent as every
        // *Db connection string in this repository.
        var connectionString = builder.Configuration.GetConnectionString("RabbitMq");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            cfg.Host(new Uri(connectionString));
        }
        else
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            });
        }

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.UseServiceDefaults();

// Authenticate/authorize before tenant resolution — an unauthenticated request is rejected before
// spending any effort resolving a tenant or touching persistence.
app.UseIdentityValidation();

app.UseTenancy();
app.MapHealthCheckEndpoints();
app.MapOrderEndpoints();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
