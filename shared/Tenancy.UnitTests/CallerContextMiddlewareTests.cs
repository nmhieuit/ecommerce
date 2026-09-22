using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tenancy.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa contracts/subject-id-header.md: every hop past the gateway reads the
/// subject the gateway resolved and logs it, and never derives or defaults one of its own.
/// </summary>
public class CallerContextMiddlewareTests
{
    /// <summary>
    /// Kiểm tra: request có header `X-Subject-Id` thì middleware gán đúng subject vào
    /// `CallerContext`.
    /// Lý do: nhánh happy-case của contracts/subject-id-header.md: mọi chặng sau gateway chỉ đọc
    /// subject mà gateway đã phân giải.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T012, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ResolvesTheCallerContext_FromTheInboundHeader()
    {
        var callerContext = new CallerContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = "phase1-stub-user";

        await CreateMiddleware().InvokeAsync(httpContext, callerContext);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Context phải chứa giá
        // trị header; đỏ khi middleware không đọc (sẽ ném lỗi).
        Assert.Equal("phase1-stub-user", callerContext.RequireSubjectId());
    }

    /// <summary>
    /// Kiểm tra: header vắng mặt, rỗng hoặc khoảng trắng đều để `CallerContext` ở trạng thái
    /// Unresolved.
    /// Lý do: middleware không bao giờ thay bằng 1 người gọi khác, vì người gọi bị thay thế chính
    /// là giỏ hàng của người khác.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T012, US2 (FR-006).
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

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác. Đạt khi context vẫn chưa có danh tính; đỏ khi gán giá trị
        // trắng.
        Assert.Throws<MissingCallerContextException>(() => callerContext.RequireSubjectId());
    }

    /// <summary>
    /// Kiểm tra: khi subject đã phân giải, middleware mở logging scope chứa `SubjectId`.
    /// Lý do: Constitution Principle VII: truy vết 1 request theo người thực hiện chứ không chỉ
    /// theo tenant; dùng cùng cơ chế logging-scope với tenant và correlation id.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T012, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesTheResolvedSubjectIntoTheLoggingScope()
    {
        var logger = new RecordingLogger();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CallerContextMiddleware.HeaderName] = "phase1-stub-user";

        await CreateMiddleware(logger).InvokeAsync(httpContext, new CallerContext());

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn. Đúng 1 scope log.
        var scope = Assert.Single(logger.Scopes);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Equal so với subject. Đỏ
        // khi thiếu khoá hoặc sai giá trị.
        Assert.Equal("phase1-stub-user", Assert.Contains("SubjectId", scope));
    }

    /// <summary>
    /// Kiểm tra: request chưa phân giải được subject thì không mở scope `SubjectId` nào.
    /// Lý do: không được ghi `SubjectId` rỗng vào log như thể người gọi có tồn tại — việc vắng mặt
    /// chính là tín hiệu.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T012, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PushesNoSubjectScope_WhenTheRequestIsUnresolved()
    {
        var logger = new RecordingLogger();

        await CreateMiddleware(logger).InvokeAsync(new DefaultHttpContext(), new CallerContext());

        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Đạt khi không mở scope
        // nào; đỏ khi vẫn ghi scope danh tính.
        Assert.Empty(logger.Scopes);
    }

    /// <summary>
    /// Kiểm tra: dù có subject hay không, middleware luôn gọi tiếp pipeline.
    /// Lý do: health probe đi thẳng vào service không qua gateway và hợp lệ khi không có người gọi;
    /// việc bắt buộc có người gọi thuộc về các route cần nó, không phải middleware đọc header.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T012, US2 (FR-006).
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

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đạt khi middleware kế tiếp
        // đã chạy; đỏ khi middleware chặn request.
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
