using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true; // carries CorrelationIdMiddleware's scope onto every log line
            logging.AddOtlpExporter();
        });

        builder.Services.AddTransient<CorrelationIdMiddleware>();

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
}
