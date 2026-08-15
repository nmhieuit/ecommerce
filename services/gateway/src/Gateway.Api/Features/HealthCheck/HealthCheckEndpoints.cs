using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.Api.Features.HealthCheck;

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

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // A healthy check carries no description; the contract omits the field rather than
        // reporting it as null.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Registers the health-check services. No readiness check is registered here, and that is a
    /// decision rather than an omission: the gateway owns no database (plan.md Technical Context —
    /// Storage: N/A), and it deliberately does not probe the BFF either. Readiness gates whether
    /// this instance receives traffic at all; making it depend on a downstream would pull the
    /// gateway out of rotation during a BFF outage, when its job is precisely to still answer and
    /// return a clear, bounded error (spec FR-006, US3). Downstream trouble is reported per request,
    /// not by this instance disappearing.
    /// </summary>
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Maps the two probes Kubernetes and local verification consume. Both are anonymous — probes
    /// cannot present a token (documented exception, plan.md Constitution Check, Principle VI).
    /// </summary>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        // Liveness answers only "is this process responding at all".
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteLivenessResponse,
        });

        // Readiness answers "can this service actually serve traffic right now". For a stateless
        // proxy that is true as soon as the process is accepting requests, so the check set is
        // empty and the report is Healthy — see AddHealthCheckFeature for why it stays that way.
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
