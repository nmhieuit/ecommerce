using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Api.Data;

namespace Orders.Api.Features.HealthCheck;

/// <summary>
/// The liveness and readiness capability, whole: check registration, route mapping, and response
/// shape live together in this one folder rather than being split across technical-layer folders
/// (spec SC-004, constitution's vertical-slice default).
/// Response contract: <c>specs/001-scaffold-service-shells/contracts/health-check.md</c>.
/// </summary>
public static class HealthCheckEndpoints
{
    /// <summary>Marks the checks that gate readiness. Liveness deliberately runs none of them.</summary>
    private const string ReadyTag = "ready";

    /// <summary>The contract's name for this service's own-database connectivity check.</summary>
    private const string SelfDatabaseCheck = "self-database";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // A healthy check carries no description; the contract omits the field rather than
        // reporting it as null.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Registers the readiness check against this service's own database only.
    /// </summary>
    /// <remarks>
    /// Opens a raw connection rather than resolving <see cref="OrdersDbContext"/>, which it used to
    /// do via <c>AddDbContextCheck</c>. Since 003-stub-identity-tenant-context the context cannot be
    /// constructed without a resolved tenant, and a probe has none — Kubernetes calls
    /// <c>/health/ready</c> directly, never through the gateway. Giving the probe a tenant of its own
    /// would have meant inventing exactly the default tenant constitution Principle V forbids.
    /// </remarks>
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionStringFactory: serviceProvider =>
                    serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("OrdersDb")
                    ?? string.Empty,
                name: SelfDatabaseCheck,
                tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Maps the two probes Kubernetes and local verification consume. Both are explicitly
    /// <c>AllowAnonymous</c> — probes cannot present a token — which matters now that
    /// <c>AddIdentityValidation</c> (014-identity-server-auth; research.md Decision 6) installs a
    /// deny-by-default <c>FallbackPolicy</c>: without it, these endpoints would inherit that policy
    /// like any other unmarked endpoint and probes would start failing with 401.
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Liveness answers only "is this process responding at all". It must not depend on the
        // database: a database outage would otherwise restart pods that are perfectly healthy.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteLivenessResponse,
        }).AllowAnonymous();

        // Readiness answers "can this service actually serve traffic right now", which includes
        // reaching its own database — it must fail closed, never report healthy without it
        // (spec FR-003 and Edge Cases).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReadinessResponse,
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteLivenessResponse(HttpContext context, HealthReport report) =>
        WriteJsonAsync(context, new { status = report.Status.ToString() });

    private static Task WriteReadinessResponse(HttpContext context, HealthReport report) =>
        WriteJsonAsync(context, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description ?? entry.Value.Exception?.Message,
            }).ToArray(),
        });

    private static Task WriteJsonAsync<TPayload>(HttpContext context, TPayload payload)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(payload, ResponseJsonOptions);
    }
}
