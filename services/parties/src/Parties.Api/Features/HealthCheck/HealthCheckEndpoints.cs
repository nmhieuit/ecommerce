using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Parties.Api.Data;

namespace Parties.Api.Features.HealthCheck;

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
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<PartiesDbContext>(
                SelfDatabaseCheck,
                tags: [ReadyTag],
                customTestQuery: ProbeDatabaseAsync);

        return services;
    }

    /// <summary>
    /// Opens a real connection to this service's own database. EF's default <c>CanConnectAsync</c>
    /// probe collapses every failure into a bare <c>false</c>; letting the connection error surface
    /// instead is what lets the 503 body carry the failure detail the contract specifies.
    /// </summary>
    private static async Task<bool> ProbeDatabaseAsync(
        PartiesDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await dbContext.Database.CloseConnectionAsync();
        return true;
    }

    /// <summary>
    /// Maps the two probes Kubernetes and local verification consume. Both are anonymous — probes
    /// cannot present a token (documented exception, plan.md Constitution Check, Principle VI).
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Liveness answers only "is this process responding at all". It must not depend on the
        // database: a database outage would otherwise restart pods that are perfectly healthy.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteLivenessResponse,
        });

        // Readiness answers "can this service actually serve traffic right now", which includes
        // reaching its own database — it must fail closed, never report healthy without it
        // (spec FR-003 and Edge Cases).
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
