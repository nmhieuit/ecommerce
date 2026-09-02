using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bff.Api.Features.HealthCheck;

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
    /// decision rather than an omission: the BFF owns no database (plan.md Technical Context —
    /// Storage: N/A), and it deliberately does not probe products/baskets/orders/parties either.
    /// Readiness gates whether this instance receives traffic at all; making it depend on the four
    /// downstream services would pull the BFF out of rotation whenever any one of them is down —
    /// including for the routes that do not touch it — when its job is precisely to still answer and
    /// return a clear, bounded ProblemDetails (spec FR-006, US3). Downstream trouble is reported per
    /// request by the resilience pipeline, not by this instance disappearing.
    /// </summary>
    public static IServiceCollection AddHealthCheckFeature(this IServiceCollection services)
    {
        services.AddHealthChecks();

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
        // Liveness answers only "is this process responding at all".
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteLivenessResponse,
        }).AllowAnonymous();

        // Readiness answers "can this service actually serve traffic right now". For a stateless
        // aggregation layer that is true as soon as the process is accepting requests, so the check
        // set is empty and the report is Healthy — see AddHealthCheckFeature for why it stays so.
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
