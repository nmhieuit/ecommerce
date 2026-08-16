using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa contracts/subject-id-header.md: every hop past the gateway reads the
/// subject the gateway resolved and logs it, and never derives or defaults one of its own.
/// </summary>
public class CallerContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ResolvesTheCallerContext_FromTheInboundHeader()
    {
        var callerContext = new CallerContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = "phase1-stub-user";

        await CreateMiddleware().InvokeAsync(httpContext, callerContext);

        Assert.Equal("phase1-stub-user", callerContext.RequireSubjectId());
    }

    /// <summary>
    /// Absent and empty are the same thing — Unresolved. The middleware never substitutes a caller,
    /// because a substituted caller is somebody else's basket.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvokeAsync_LeavesTheCallerContextUnresolved_WhenTheHeaderIsAbsentOrEmpty(string? headerValue)
    {
        var callerContext = new CallerContext();
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = headerValue;
        }

        await CreateMiddleware().InvokeAsync(httpContext, callerContext);

        Assert.Throws<MissingCallerContextException>(() => callerContext.RequireSubjectId());
    }

    /// <summary>
    /// Constitution Principle VII: a request is traceable by who made it, not only by which tenant
    /// it belonged to. Same logging-scope mechanism the tenant and correlation id already use.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesTheResolvedSubjectIntoTheLoggingScope()
    {
        var logger = new RecordingLogger();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = "phase1-stub-user";

        await CreateMiddleware(logger).InvokeAsync(httpContext, new CallerContext());

        var scope = Assert.Single(logger.Scopes);
        Assert.Equal("phase1-stub-user", Assert.Contains("SubjectId", scope));
    }

    /// <summary>
    /// An unresolved request must not log a blank SubjectId as though a caller existed — the
    /// absence is the signal.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesNoSubjectScope_WhenTheRequestIsUnresolved()
    {
        var logger = new RecordingLogger();

        await CreateMiddleware(logger).InvokeAsync(new DefaultHttpContext(), new CallerContext());

        Assert.Empty(logger.Scopes);
    }

    /// <summary>
    /// The pipeline always continues. Failing here would break the health probes, which reach a
    /// service without going through the gateway and legitimately have no caller — enforcement
    /// belongs at the routes that need a caller, not at the middleware that reads one.
    /// </summary>
    [Theory]
    [InlineData("phase1-stub-user")]
    [InlineData(null)]
    public async Task InvokeAsync_AlwaysCallsTheRestOfThePipeline(string? headerValue)
    {
        var called = false;
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = headerValue;
        }

        var middleware = new CallerContextMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            new RecordingLogger());

        await middleware.InvokeAsync(httpContext, new CallerContext());

        Assert.True(called);
    }

    private static CallerContextMiddleware CreateMiddleware(ILogger<CallerContextMiddleware>? logger = null) =>
        new(_ => Task.CompletedTask, logger ?? new RecordingLogger());

    /// <summary>
    /// Captures the scopes the middleware opens, so "it logs the subject" is asserted rather than
    /// assumed. Mirrors the recorder in <see cref="TenantContextMiddlewareTests"/>.
    /// </summary>
    private sealed class RecordingLogger : ILogger<CallerContextMiddleware>
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
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
