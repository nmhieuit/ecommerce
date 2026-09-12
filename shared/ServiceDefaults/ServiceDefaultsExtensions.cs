using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

/// <summary>
/// Shared cross-cutting wiring every service in the platform uses identically — constitution
/// Principle VII: "observability MUST NOT be configured per service by hand." Call
/// <see cref="AddServiceDefaults"/> before <c>Build()</c> and <see cref="UseServiceDefaults"/>
/// on the built <see cref="WebApplication"/>, in every service's <c>Program.cs</c>.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Registers OpenTelemetry traces and metrics, exported via OTLP to the platform's
    /// OTel Collector (which forwards to the Elastic stack), and registers the correlation-ID
    /// middleware for DI. Structured logging (never interpolated strings, per Principle VII) is
    /// enabled through the standard <c>ILogger</c> pipeline every ASP.NET Core host already has;
    /// this method attaches OTLP log export to it rather than replacing it.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // 020-timeouts-retry-circuit-breaker (research.md Decision 6): every attempt,
                // retry, and circuit-breaker state change Microsoft.Extensions.Http.Resilience
                // (Polly v8) makes is emitted under this activity source. Without it, resilience
                // events happen but never reach Elastic — only the final outcome would, via the
                // HttpClient instrumentation above, which cannot distinguish "failed on the first
                // try" from "failed after two retries and an open circuit" (spec FR-008).
                .AddSource("Polly")
                // 024-verify-transactional-outbox: MassTransit emits its own ActivitySource under
                // this name for publish/consume/outbox-delivery activity — without it, a feature
                // whose entire point is crash/idempotency behaviour would be invisible in traces,
                // failing "a feature is not complete until it is debuggable in production from
                // telemetry alone" (constitution Principle VII).
                .AddSource("MassTransit")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // See the "Polly" activity source above — same reasoning, for the meter side.
                .AddMeter("Polly")
                .AddMeter("MassTransit")
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true; // carries CorrelationIdMiddleware's scope onto every log line
            logging.AddOtlpExporter();
        });

        // No DI registration for CorrelationIdMiddleware: UseMiddleware<T>() constructs
        // conventional middleware itself via ActivatorUtilities, injecting RequestDelegate
        // specially — registering it in the container as well breaks DI validation, since
        // RequestDelegate isn't (and shouldn't be) a resolvable service.

        return builder;
    }

    /// <summary>
    /// Wires the correlation-ID middleware into the request pipeline. Call this immediately
    /// after <c>Build()</c>, before any endpoint mapping, so the correlation ID exists for the
    /// full lifetime of every request (constitution Principle VII: generated at the edge,
    /// propagated across every call and message, including through the frontend).
    /// </summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    /// <summary>
    /// Declares the secrets this service cannot start without (specs/018-cluster-secret-store
    /// FR-002/FR-007; constitution Principle VI). Registers <see cref="RequiredSecretsValidator"/>
    /// and calls <c>ValidateOnStart()</c>, so the generic host validates every declared secret
    /// during startup — before <c>app.Run()</c> ever accepts a request — and fails with a
    /// structured error naming the missing secret(s) if any are absent or blank. Call once per
    /// service, right after <see cref="AddServiceDefaults{TBuilder}"/>, passing every secret that
    /// service's own configuration requires (e.g. <c>RequiredSecret.ConnectionString("OrdersDb")</c>).
    /// </summary>
    public static TBuilder AddRequiredSecretsValidation<TBuilder>(this TBuilder builder, params RequiredSecret[] secrets)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IValidateOptions<RequiredSecretsOptions>, RequiredSecretsValidator>();
        builder.Services.AddOptions<RequiredSecretsOptions>()
            .Configure(options => options.Secrets = secrets)
            .ValidateOnStart();

        return builder;
    }
}
