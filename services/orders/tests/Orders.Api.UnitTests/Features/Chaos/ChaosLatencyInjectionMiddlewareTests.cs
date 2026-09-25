using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orders.Api.Features.Chaos;

namespace Orders.Api.UnitTests.Features.Chaos;

/// <summary>
/// Spec 025 (diễn tập chaos engineering) — các bất biến của
/// `specs/025-chaos-pod-kill-latency/contracts/chaos-latency-injection-contract.md` mà 1 bài tập chaos
/// dựa vào phải đúng TRƯỚC khi <see cref="ChaosLatencyInjectionMiddleware"/> được nhắm vào 1 cluster
/// thật. Dùng <see cref="RecordingChaosDelay"/> thay cho `Task.Delay` thật nên chạy tức thì và xác định
/// (`research.md` Quyết định 4). Chỉ kiểm tra middleware đứng riêng: KHÔNG kiểm tra thứ tự đăng ký trong
/// `Program.cs` (Bất biến 5) hay giá trị mặc định trong `appsettings.json` (Bất biến 1) — xem QA_Debt mục 025.
/// </summary>
public class ChaosLatencyInjectionMiddlewareTests
{
    private static readonly IOptions<ChaosOptions> Enabled =
        Options.Create(new ChaosOptions { AllowLatencyInjection = true });

    private static readonly IOptions<ChaosOptions> Disabled =
        Options.Create(new ChaosOptions { AllowLatencyInjection = false });

    /// <summary>
    /// Kiểm tra: khi cờ `AllowLatencyInjection = false`, header `X-Chaos-Latency-Ms: 2000` bị bỏ qua
    /// hoàn toàn — không có yêu cầu trì hoãn nào được ghi nhận.
    /// Lý do (phải test): Bất biến 1 / FR-006 — mặc định (và mọi cấu hình production) phải KHÔNG bao giờ
    /// trì hoãn theo header của caller; nếu hỏng, bất kỳ ai gửi header đều làm chậm được service thật.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, Bất biến 1.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Disabled);

        // Assert.Null: xanh khi không có khoảng trễ nào được yêu cầu dù header hợp lệ; đỏ khi middleware
        // vẫn trì hoãn lúc cờ tắt (bỏ điều kiện `AllowLatencyInjection` sẽ làm test này đỏ).
        Assert.Null(delay.RequestedDuration);
    }

    /// <summary>
    /// Kiểm tra: cờ bật nhưng request không có header thì không có độ trễ nhân tạo.
    /// Lý do (phải test): Bất biến 2 — chỉ request nào chủ động gửi header mới bị làm chậm; mọi request
    /// khác (kể cả tải nền của bài tập) phải chạy bình thường.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, Bất biến 2.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_EnabledWithoutHeader_DoesNotDelay()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Null: xanh khi không có yêu cầu trì hoãn; đỏ khi middleware tự trì hoãn dù không có header.
        Assert.Null(delay.RequestedDuration);
    }

    /// <summary>
    /// Kiểm tra: cờ bật + header `X-Chaos-Latency-Ms: 2000` → middleware yêu cầu trì hoãn đúng 2000 ms.
    /// Lý do (phải test): FR-002/US2 — đây là năng lực cốt lõi của công cụ tiêm độ trễ; sai số ở đây
    /// làm sai toàn bộ số liệu SLO của bài tập.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, FR-002.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_EnabledWithValidHeader_DelaysByRequestedAmount()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Equal(mong đợi, thực tế): xanh khi khoảng trễ được yêu cầu đúng 2000 ms; đỏ khi khác
        // (không trì hoãn — null, hoặc sai đơn vị/giá trị).
        Assert.Equal(TimeSpan.FromMilliseconds(2000), delay.RequestedDuration);
    }

    /// <summary>
    /// Kiểm tra: header `999999` (vượt trần) bị kẹp về đúng `MaxInjectedLatencyMs` (30 000 ms).
    /// Lý do (phải test): Bất biến 4 — trần an toàn chặn 1 lần gõ nhầm số 0 biến bài tập có kiểm soát
    /// thành treo hàng chục phút.
    /// Lưu ý: chỉ thử 1 giá trị xa trần (999999), không thử sát biên 30 000/30 001.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, Bất biến 4.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_HeaderAboveSafetyCap_ClampsToMax()
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader("999999");

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Equal: xanh khi khoảng trễ bị kẹp đúng bằng `MaxInjectedLatencyMs`; đỏ khi trì hoãn lâu
        // hơn trần (bỏ `Math.Min` sẽ làm test này đỏ) hoặc không trì hoãn.
        Assert.Equal(TimeSpan.FromMilliseconds(ChaosOptions.MaxInjectedLatencyMs), delay.RequestedDuration);
    }

    /// <summary>
    /// Kiểm tra: header không phải số ("not-a-number"), số âm ("-500") hoặc bằng 0 đều được coi như
    /// vắng mặt — không có độ trễ và không ném lỗi.
    /// Lý do (phải test): Bất biến 3 — 1 giá trị sai không được làm request thất bại; lỗi thao tác của
    /// người chạy bài tập không được biến thành lỗi cho người dùng.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, Bất biến 3.
    /// </summary>
    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-500")]
    [InlineData("0")]
    public async Task InvokeAsync_InvalidOrNonPositiveHeader_TreatedAsAbsent(string headerValue)
    {
        var delay = new RecordingChaosDelay();
        var middleware = new ChaosLatencyInjectionMiddleware(NextThatSucceeds, delay);
        var context = CreateContextWithHeader(headerValue);

        await middleware.InvokeAsync(context, Enabled);

        // Assert.Null: xanh khi giá trị không hợp lệ/không dương không sinh yêu cầu trì hoãn (và
        // `InvokeAsync` không ném exception); đỏ khi vẫn trì hoãn hoặc ném lỗi.
        Assert.Null(delay.RequestedDuration);
    }

    /// <summary>
    /// Kiểm tra: dù có tiêm độ trễ (cờ bật + header hợp lệ), middleware luôn gọi tiếp `next` của pipeline.
    /// Lý do (phải test): Bất biến 6 — middleware chỉ đổi THỜI ĐIỂM pipeline chạy, không bao giờ nuốt
    /// request hay đổi kết quả trả về.
    /// Lưu ý: chỉ khẳng định `next` được gọi, không so sánh status/header/body response.
    /// Task nguồn: spec 025 (diễn tập chaos engineering) — US2, Bất biến 6.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_RegardlessOfInjection()
    {
        var nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new ChaosLatencyInjectionMiddleware(Next, new RecordingChaosDelay());
        var context = CreateContextWithHeader("2000");

        await middleware.InvokeAsync(context, Enabled);

        // Assert.True: xanh khi pipeline phía sau vẫn được gọi sau khi trì hoãn; đỏ khi middleware chặn
        // request (không gọi `next`).
        Assert.True(nextCalled);
    }

    private static Task NextThatSucceeds(HttpContext context) => Task.CompletedTask;

    private static DefaultHttpContext CreateContextWithHeader(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ChaosLatencyInjectionMiddleware.LatencyHeaderName] = value;
        return context;
    }

    private sealed class RecordingChaosDelay : IChaosDelay
    {
        public TimeSpan? RequestedDuration { get; private set; }

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            RequestedDuration = duration;
            return Task.CompletedTask;
        }
    }
}
