using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy.UnitTests;

/// <summary>
/// Spec FR-003/FR-006: every hop past the gateway reads the tenant the gateway resolved, and that
/// tenant shows up on the hop's structured logs — it never re-derives or defaults one.
/// </summary>
public class TenantContextMiddlewareTests
{
    /// <summary>
    /// Kiểm tra: khi request đến có header `X-Tenant-Id: acme`, middleware gán đúng "acme" vào
    /// `TenantContext`.
    /// Lý do phải test: đây là nhánh happy-case của FR-003 — service chỉ đọc tenant mà gateway đã phân
    /// giải, không tự suy luận lại.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T006, nền tảng cho US1/US2.
    /// </summary>
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
    /// Kiểm tra: header `X-Tenant-Id` vắng mặt, rỗng hoặc chỉ có khoảng trắng đều khiến
    /// `TenantContext` vẫn ở trạng thái Unresolved (`RequireTenantId()` ném exception).
    /// Lý do phải test: theo contracts/tenant-id-header.md (Failure Modes), "vắng" và "rỗng" là cùng 1
    /// trạng thái — Unresolved. Tuyệt đối không có tenant dự phòng, vì đó chính là mục đích của tính
    /// năng này.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T006, nền tảng cho US1/US2.
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
    /// Kiểm tra: khi tenant đã phân giải, middleware mở đúng 1 logging scope chứa `TenantId = "acme"`.
    /// Lý do phải test: FR-006 / Constitution Principle VII — tenant phải hiện trong mọi dòng log có
    /// cấu trúc của request, để truy vết 1 request xuyên các chặng theo tenant. Dùng cùng cơ chế
    /// logging-scope mà `CorrelationIdMiddleware` đã dùng nên không cần cấu hình riêng ở từng service.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T006, US1 (US1-KB2).
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
    /// Kiểm tra: request chưa phân giải được tenant thì middleware KHÔNG mở logging scope nào.
    /// Lý do phải test: không được ghi 1 `TenantId` rỗng/null vào log như thể tenant có tồn tại — việc
    /// vắng mặt chính là tín hiệu để người vận hành nhận ra request bị thiếu tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T006, nền tảng cho US1/US2.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesNoTenantScope_WhenTheRequestIsUnresolved()
    {
        var logger = new RecordingLogger();

        await CreateMiddleware(logger).InvokeAsync(new DefaultHttpContext(), new TenantContext());

        Assert.Empty(logger.Scopes);
    }

    /// <summary>
    /// Kiểm tra: dù có tenant ("acme") hay không (null), middleware luôn gọi tiếp phần còn lại của
    /// pipeline.
    /// Lý do phải test: middleware này chỉ đọc và ghi nhận tenant, không được tự chặn request — việc
    /// từ chối khi thiếu tenant là của cổng persistence ở `AddDbContext`. Nếu nó chặn sớm thì health
    /// endpoint (không cần tenant) cũng sẽ hỏng theo.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T006, nền tảng cho US1/US2.
    /// </summary>
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
