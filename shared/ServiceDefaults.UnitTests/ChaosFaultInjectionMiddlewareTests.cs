using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ServiceDefaults;

namespace ServiceDefaults.UnitTests;

/// <summary>
/// Spec 027 (chính sách ngân sách lỗi, SCRUM-35) — các bất biến 2, 3, 4, 6 của
/// `specs/027-error-budget-alerting/contracts/chaos-fault-injection-contract.md`. Chỉ kiểm tra
/// middleware đứng riêng: Bất biến 1 (mặc định `false` trong cấu hình) và Bất biến 5 (span 500 tới
/// được Elasticsearch qua OTel) được xác thực trên stack thật theo quickstart.md Kịch bản 1.
/// </summary>
public class ChaosFaultInjectionMiddlewareTests
{
    private static readonly IOptions<ChaosFaultOptions> Enabled =
        Options.Create(new ChaosFaultOptions { AllowFaultInjection = true });

    private static readonly IOptions<ChaosFaultOptions> Disabled =
        Options.Create(new ChaosFaultOptions { AllowFaultInjection = false });

    /// <summary>
    /// Kiểm tra: khi cờ `AllowFaultInjection = false`, header `X-Chaos-Fault: 5xx` bị bỏ qua — phần còn
    /// lại của pipeline vẫn chạy và response giữ nguyên.
    /// Lý do (phải test): Bất biến 2 — middleware có mặt ở cả 7 service; nếu cờ tắt mà header vẫn có hiệu
    /// lực thì bất kỳ ai gửi header đều làm hỏng được service thật và đốt ngân sách lỗi của nó.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-014, FR-015, Bất biến 2.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely()
    {
        var next = new RecordingNext();
        var middleware = new ChaosFaultInjectionMiddleware(next.InvokeAsync);
        var context = CreateContextWithHeader("5xx");

        await middleware.InvokeAsync(context, Disabled);

        // Assert.True: xanh khi phần còn lại của pipeline vẫn chạy; đỏ khi middleware chặn request dù cờ tắt.
        Assert.True(next.WasCalled);

        // Assert.Equal(mong đợi, thực tế): xanh khi response là của pipeline (200), không phải 500 tiêm vào.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: cờ bật + header `X-Chaos-Fault: 5xx` → response 500 và pipeline phía sau KHÔNG chạy.
    /// Lý do (phải test): Bất biến 3 / FR-014 — đây là năng lực cốt lõi để làm cạn ngân sách 5xx có chủ
    /// đích; không gọi tiếp pipeline nghĩa là lỗi tiêm vào không chạm dữ liệu nghiệp vụ nào.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-014, Bất biến 3.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_EnabledWithFaultHeader_Returns500WithoutCallingNext()
    {
        var next = new RecordingNext();
        var middleware = new ChaosFaultInjectionMiddleware(next.InvokeAsync);
        var context = CreateContextWithHeader("5xx");

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Equal(mong đợi, thực tế): xanh khi response là 500; đỏ khi middleware không tiêm lỗi.
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        // Assert.False: xanh khi pipeline phía sau không chạy; đỏ khi request vẫn đi tới endpoint thật.
        Assert.False(next.WasCalled);
    }

    /// <summary>
    /// Kiểm tra: cờ bật nhưng request không có header → request đi tiếp nguyên vẹn.
    /// Lý do (phải test): Bất biến 4 — trong lúc diễn tập, chỉ request chủ động gửi header mới bị hỏng;
    /// tải nền và health probe phải chạy bình thường.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-015, Bất biến 4.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_EnabledWithoutHeader_CallsNext()
    {
        var next = new RecordingNext();
        var middleware = new ChaosFaultInjectionMiddleware(next.InvokeAsync);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Enabled);

        // Assert.True: xanh khi pipeline vẫn chạy; đỏ khi middleware tự tiêm lỗi dù không có header.
        Assert.True(next.WasCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: cờ bật + header mang giá trị khác `5xx` (vd `500`, `true`, rỗng) → request đi tiếp.
    /// Lý do (phải test): Bất biến 4 — chỉ một giá trị duy nhất có hiệu lực, để một header gõ nhầm không
    /// âm thầm biến thành lỗi thật.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-015, Bất biến 4.
    /// </summary>
    [Theory]
    [InlineData("500")]
    [InlineData("true")]
    [InlineData("")]
    [InlineData("5XX ")]
    public async Task InvokeAsync_EnabledWithOtherHeaderValue_CallsNext(string headerValue)
    {
        var next = new RecordingNext();
        var middleware = new ChaosFaultInjectionMiddleware(next.InvokeAsync);
        var context = CreateContextWithHeader(headerValue);

        await middleware.InvokeAsync(context, Enabled);

        // Assert.True: xanh khi pipeline vẫn chạy với giá trị header không hợp lệ.
        Assert.True(next.WasCalled);
    }

    /// <summary>
    /// Kiểm tra: khi không tiêm lỗi, response do pipeline phía sau tạo (status + header) đi ra nguyên vẹn.
    /// Lý do (phải test): Bất biến 6 / FR-015 — middleware nằm trên đường đi của mọi request ở cả 7
    /// service; nó không được thay đổi bất kỳ response nào khi không có lệnh tiêm lỗi.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-015, Bất biến 6.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NoInjection_LeavesDownstreamResponseUntouched()
    {
        var middleware = new ChaosFaultInjectionMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers["X-Downstream"] = "kept";
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Equal(mong đợi, thực tế): xanh khi status và header của pipeline phía sau còn nguyên.
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("kept", context.Response.Headers["X-Downstream"].ToString());
    }

    private static DefaultHttpContext CreateContextWithHeader(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ChaosFaultInjectionMiddleware.FaultHeaderName] = value;
        return context;
    }

    private sealed class RecordingNext
    {
        public bool WasCalled { get; private set; }

        public Task InvokeAsync(HttpContext context)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }
}
