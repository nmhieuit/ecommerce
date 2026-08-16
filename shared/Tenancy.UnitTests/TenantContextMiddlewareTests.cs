using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy.UnitTests;

/// <summary>
/// Spec FR-003/FR-006: every hop past the gateway reads the tenant the gateway resolved, and that
/// tenant shows up on the hop's structured logs — it never re-derives or defaults one.
/// </summary>
public class TenantContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ResolvesTheTenantContext_FromTheInboundHeader()
    {
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantContextMiddleware.HeaderName] = "acme";

        await CreateMiddleware().InvokeAsync(httpContext, tenantContext);

        Assert.Equal("acme", tenantContext.RequireTenantId());
    }

    /// <summary>
    /// contracts/tenant-id-header.md Failure Modes: absent and empty are the same thing —
    /// Unresolved. Never a fallback tenant, which is the whole point of the feature.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvokeAsync_LeavesTheTenantContextUnresolved_WhenTheHeaderIsAbsentOrEmpty(string? headerValue)
    {
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[TenantContextMiddleware.HeaderName] = headerValue;
        }

        await CreateMiddleware().InvokeAsync(httpContext, tenantContext);

        Assert.Throws<MissingTenantContextException>(() => tenantContext.RequireTenantId());
    }

    /// <summary>
    /// Constitution Principle VII, via the same logging-scope mechanism
    /// <c>CorrelationIdMiddleware</c> already uses, so the tenant lands on every structured log
    /// line for the request with no per-service wiring.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesTheResolvedTenantIntoTheLoggingScope()
    {
        var logger = new RecordingLogger();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantContextMiddleware.HeaderName] = "acme";

        await CreateMiddleware(logger).InvokeAsync(httpContext, new TenantContext());

        var scope = Assert.Single(logger.Scopes);
        Assert.Equal("acme", Assert.Contains("TenantId", scope));
    }

    /// <summary>
    /// An unresolved request must not log a blank or null TenantId as though one existed —
    /// the absence is the signal.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesNoTenantScope_WhenTheRequestIsUnresolved()
    {
        var logger = new RecordingLogger();

        await CreateMiddleware(logger).InvokeAsync(new DefaultHttpContext(), new TenantContext());

        Assert.Empty(logger.Scopes);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData(null)]
    public async Task InvokeAsync_AlwaysCallsTheRestOfThePipeline(string? headerValue)
    {
        var called = false;
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[TenantContextMiddleware.HeaderName] = headerValue;
        }

        var middleware = new TenantContextMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            new RecordingLogger());

        await middleware.InvokeAsync(httpContext, new TenantContext());

        Assert.True(called);
    }

    private static TenantContextMiddleware CreateMiddleware(ILogger<TenantContextMiddleware>? logger = null) =>
        new(_ => Task.CompletedTask, logger ?? new RecordingLogger());

    /// <summary>
    /// Captures the scopes the middleware opens, so "it logs the tenant" can be asserted rather
    /// than assumed. Only dictionary-shaped scopes are recorded — the shape
    /// <c>CorrelationIdMiddleware</c> established.
    /// </summary>
    private sealed class RecordingLogger : ILogger<TenantContextMiddleware>
    {
        public List<IReadOnlyDictionary<string, object>> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IReadOnlyDictionary<string, object> values)
            {
                Scopes.Add(values);
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Nothing under test asserts on log messages, only on scopes.
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
                // No scope state to unwind in a recording logger.
            }
        }
    }
}
