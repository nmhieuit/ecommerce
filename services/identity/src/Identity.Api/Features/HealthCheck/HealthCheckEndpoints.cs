using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Identity.Api.Features.HealthCheck;

/// <summary>
/// The liveness and readiness capability, whole: check registration, route mapping, and response
/// shape live together in this one folder rather than being split across technical-layer folders
/// (constitution's vertical-slice default) — mirrors every other service's
/// <c>Features/HealthCheck/HealthCheckEndpoints.cs</c>.
/// </summary>
public static class HealthCheckEndpoints
{
    private const string ReadyTag = "ready";

    /// <summary>The contract's name for this service's own-database connectivity check.</summary>
    private const string SelfDatabaseCheck = "self-database";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Registers the readiness check against this service's own "identity" database — added once
    /// User Story 1 (tasks.md T020-T021) gave this service a database at all. A raw connection,
    /// not <c>AddDbContextCheck&lt;T&gt;</c>, matching every domain service's pattern (mirrors
    /// <c>Parties.Api.Features.HealthCheck.HealthCheckEndpoints</c>) — this service happens to have
    /// no tenant-gating reason to avoid <c>AddDbContextCheck</c>, but staying consistent with the
    /// platform's one health-check shape is worth more than the marginal simplification.
    /// </summary>
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionStringFactory: serviceProvider =>
                    serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("IdentityDb")
                    ?? string.Empty,
                name: SelfDatabaseCheck,
                tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Maps the two probes Kubernetes and local verification consume. Both are anonymous — probes
    /// cannot present a token (plan.md Constitution Check, Principle VI).
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteLivenessResponse,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReadinessResponse,
        });

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
