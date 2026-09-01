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

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Registers the health-check services. No readiness check is registered yet: this service owns
    /// no database at shell-scaffolding time (tasks.md T001) — a database-backed readiness check
    /// arrives once User Story 1 (tasks.md T020-T021) adds the identity/user stores.
    /// </summary>
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks();

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
