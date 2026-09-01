using Identity.Api.Data;
using Identity.Api.Features.HealthCheck;
using Identity.Api.HostedIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Read lazily, inside each options callback below, rather than once into a local here — a
// WebApplicationFactory-based test injects its connection-string override onto builder.Configuration
// only at Build() time, so a value captured any earlier would miss it and silently fall back to the
// unreachable "identity-db" hostname (verified empirically: this was exactly that bug, caught by
// Identity.Api.IntegrationTests failing with a TCP connect timeout even against a healthy
// Testcontainers instance). Parties.Api.Program.cs already reads its connection string the same
// lazy way, for the same reason.
static string? IdentityDbConnectionString(IConfiguration configuration) =>
    configuration.GetConnectionString("IdentityDb");

// The user credential store (data-model.md — Identity User; research.md Decision 8) — its own
// DbContext, its own migrations history table, separate from Duende's two stores below even
// though all three share the "identity" database (Data/MigrationsHistoryTables.cs).
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(
        IdentityDbConnectionString(builder.Configuration),
        sql => sql.MigrationsHistoryTable(MigrationsHistoryTables.Identity)));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
    .AddDefaultTokenProviders();

// The identity server itself (ADR-0001; research.md Decision 1), backed by its own two EF stores
// (tasks.md T020) and ASP.NET Identity as the user store (T021), issuing the tenant_id claim
// through TenantClaimsProfileService (T022) instead of Duende's default profile service.
builder.Services.AddIdentityServer(options =>
    {
        // Every token carries an explicit `aud` claim rather than relying solely on API-resource
        // scope matching — every downstream service's AddIdentityValidation() checks it
        // (shared/Identity/IdentityValidationExtensions.cs).
        options.EmitStaticAudienceClaim = true;
    })
    .AddConfigurationStore(options =>
        options.ConfigureDbContext = db =>
            db.UseSqlServer(IdentityDbConnectionString(builder.Configuration), sql => sql
                .MigrationsHistoryTable(MigrationsHistoryTables.Configuration)
                .MigrationsAssembly(MigrationsAssembly.Name)))
    .AddOperationalStore(options =>
        options.ConfigureDbContext = db =>
            db.UseSqlServer(IdentityDbConnectionString(builder.Configuration), sql => sql
                .MigrationsHistoryTable(MigrationsHistoryTables.PersistedGrant)
                .MigrationsAssembly(MigrationsAssembly.Name)))
    .AddAspNetIdentity<ApplicationUser>()
    .AddProfileService<TenantClaimsProfileService>();

builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();

// Wires Duende's own middleware pipeline: discovery document, JWKS, /connect/token, /connect/
// authorize, and (once a login UI exists — see Config.cs remarks) the interactive login/consent
// endpoints. Must run before health-check mapping so its own middleware sees every request first,
// matching every other service's UseServiceDefaults() → feature-middleware → endpoint-mapping order.
app.UseIdentityServer();

app.MapHealthCheckEndpoints();

// One-shot seeding (Data/SeedData.cs), not run on every request path — matches
// services/parties's migrator-as-a-separate-step convention. `dotnet run -- --seed` seeds and
// exits without starting the host; a normal run skips straight to app.Run().
if (args.Contains("--seed"))
{
    await SeedData.EnsureSeedDataAsync(app.Services);
    return;
}

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
