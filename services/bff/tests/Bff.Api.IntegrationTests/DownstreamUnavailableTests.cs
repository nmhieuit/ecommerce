using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTestSupport;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// US3 / spec Test Scenario 3 / FR-006 / SC-003: when a downstream service is unavailable, the
/// caller gets a clear, well-formed error within a bounded time instead of hanging.
/// </summary>
/// <remarks>
/// No downstream host is started here. Every failure below travels through the real resilience
/// pipeline and the real exception handler; only the transport underneath is substituted.
/// <para>
/// The tests that assert a *specific* status inject the transport failure, because 502 versus 504
/// turns on whether the failure occurs inside the 1 s attempt timeout — and how fast a real machine
/// reports an unreachable host is that machine's business, not ours. That was not a hypothetical:
/// asserting against a genuinely unresolvable host passed in isolation and failed intermittently
/// when the suite ran alongside other Testcontainers suites, where DNS resolution slowed past the
/// attempt timeout and a 502 became a 504. The last test keeps a real unreachable address, and
/// asserts only what is true regardless of the host's speed.
/// </para>
/// </remarks>
public class DownstreamUnavailableTests
{
    /// <summary>
    /// SC-003: "callers receive a clear error response in under 5 seconds, in 100% of observed
    /// cases". Asserted as a hard bound, not a comment — an unbounded wait is the exact failure
    /// this story exists to prevent.
    /// </summary>
    private static readonly TimeSpan ClearErrorBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A host that cannot resolve. <c>.invalid</c> is reserved by RFC 2606 precisely so it can
    /// never exist.
    /// </summary>
    private const string UnreachableAddress = "http://products-service.invalid";

    /// <summary>
    /// Kiểm tra: khi Products service không tới được (transport lỗi ngay), `GET /bff/products` trả
    /// về `502 Bad Gateway` trong dưới 5 giây.
    /// Lý do: SC-003 yêu cầu "callers nhận lỗi rõ ràng trong dưới 5 giây, 100% trường hợp quan sát
    /// được" — đo bằng ngưỡng cứng (`stopwatch`), không phải chỉ nêu trong comment, vì chờ vô hạn
    /// chính là hành vi lỗi US3 tồn tại để ngăn chặn.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T053, US3.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsBadGateway_WhenTheProductsServiceIsUnreachable()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi 500 trần hoặc mã
        // khác.
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đạt khi phản hồi nhanh (dưới
        // 5 giây); đỏ khi BFF treo chờ downstream.
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");
    }

    /// <summary>
    /// Kiểm tra: khi Products service nhận request nhưng không bao giờ trả lời (khác với không tới
    /// được), `GET /bff/products` trả về `504 Gateway Timeout` trong dưới 5 giây.
    /// Lý do: Edge Case của spec — "downstream phản hồi chậm" là 1 kiểu lỗi khác với "downstream
    /// không tồn tại", và phải có mã trạng thái riêng biệt, để người vận hành đọc dashboard phân
    /// biệt được "service đã chết" với "service đang ì ạch".
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T053, US3.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsGatewayTimeout_WhenTheProductsServiceNeverAnswers()
    {
        await using var bff = BffTestHost.CreateBffWithUnresponsiveService("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi mã khác (ví dụ
        // 502 hay 500).
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đạt khi BFF tự cắt sau ngân
        // sách timeout thay vì chờ vô hạn.
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");
    }

    /// <summary>
    /// Kiểm tra: response lỗi có `Content-Type: application/problem+json`, đủ
    /// `type`/`title`/`status` (khớp `502`), và có `correlationId` khớp đúng giá trị header
    /// `X-Correlation-Id` của response.
    /// Lý do: data-model.md yêu cầu lỗi phải là RFC 7807 ProblemDetails có `correlationId` để truy
    /// vết được trong hệ observability chung (Principle VII) — thiếu test này, lỗi có thể trả về
    /// đúng mã trạng thái nhưng body không đủ cấu trúc để ai đó thật sự tra cứu được request nào đã
    /// hỏng.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T053, US3.
    /// </summary>
    [Fact]
    public async Task ADownstreamFailure_ReturnsProblemDetailsCarryingTheCorrelationId()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync("/bff/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Equal((int)HttpStatusCode.BadGateway, problem.GetProperty("status").GetInt32());

        var correlationId = problem.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        // The same value the response header carries, or it is useless for tracing the request.
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-Id").Single(),
            correlationId);
    }

    /// <summary>
    /// Kiểm tra: body lỗi có nhắc tên logic của downstream ("ProductsApi") để còn chẩn đoán được,
    /// nhưng không chứa host/scheme/tên thư viện lỗi/stack trace nào (`downstream.test`, `http://`,
    /// `Polly`, `System.Net.Http`, `at Bff.Api`, `No such host`).
    /// Lý do: quy tắc kiểm chứng của data-model.md — lỗi "KHÔNG được chứa URL/địa chỉ nội bộ của
    /// downstream, chỉ được nêu tên logic", để lỗi vẫn chẩn đoán được mà không lộ topology ra
    /// client (nhất quán với FR-001).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T054, US3.
    /// </summary>
    [Fact]
    public async Task ADownstreamFailure_NamesTheLogicalServiceOnly_NeverItsAddress()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync("/bff/products");
        var body = await response.Content.ReadAsStringAsync();

        // Diagnosable: the caller can tell which dependency failed.
        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Body
        // PHẢI có tên logic của dependency (phân biệt hoa/thường); đỏ khi thông báo lỗi quá mơ hồ.
        Assert.Contains("ProductsApi", body, StringComparison.Ordinal);

        // But nothing about where it lives — no host, no scheme, and no stack trace.
        foreach (var leak in new[]
                 { "downstream.test", "http://", "Polly", "System.Net.Http", "at Bff.Api", "No such host" })
        {
            // Assert.DoesNotContain(phần tử, tập hợp): xanh khi tập hợp không chứa phần tử, đỏ khi
            // có.
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Kiểm tra: 3 route còn lại (baskets/orders/parties, không chỉ products) khi downstream tương
    /// ứng không tới được cũng trả về `502` + `application/problem+json`, giống hệt route sản phẩm.
    /// Lý do: mọi route phải lỗi rõ ràng như nhau — 1 route mà đường xử lý lỗi chưa được nối dây
    /// (do code mới thêm sau, quên wiring) sẽ lộ ra bằng 1 `500` trần thay vì `502` có cấu trúc, và
    /// chỉ test riêng route products thì không bắt được thiếu sót đó ở 3 route kia.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T053, US3.
    /// </summary>
    [Theory]
    [InlineData("BasketsApi", "/bff/baskets/8a1f6f6e-0000-4000-8000-000000000001")]
    [InlineData("OrdersApi", "/bff/orders/8a1f6f6e-0000-4000-8000-000000000002")]
    [InlineData("PartiesApi", "/bff/parties/8a1f6f6e-0000-4000-8000-000000000003")]
    public async Task EveryRoute_FailsAsAProblemDetails_WhenItsDownstreamIsUnreachable(
        string serviceConfigurationName,
        string route)
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport(serviceConfigurationName);
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync(route);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 502 trên cả 3
        // route; đỏ khi route nào chưa nối xử lý lỗi (500 trần).
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng kiểu
        // problem+json;
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Kiểm tra: gọi thật (không thay thế transport nào) tới 1 host thật sự không tồn tại
    /// (`products-service.invalid`, dành riêng bởi RFC 2606) — response phải là `502` hoặc `504`
    /// (chấp nhận cả 2), kiểu `application/problem+json`, trong dưới 5 giây, và `detail` có nêu tên
    /// "ProductsApi" kèm `correlationId`.
    /// Lý do: 5 test phía trên đều thay thế transport để ép ra đúng 1 mã lỗi cụ thể; test này là
    /// bằng chứng đối chứng bằng transport thật — chấp nhận cả `502` lẫn `504` vì việc rơi vào mã
    /// nào phụ thuộc tốc độ phân giải DNS của từng máy (đã quan sát thấy suite bị flaky khi ép cứng
    /// 1 mã lúc chạy song song với suite khác), nhưng vẫn khẳng định đúng phần bất biến: có giới
    /// hạn thời gian, có cấu trúc, có nêu tên dependency.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T053, US3.
    /// </summary>
    [Fact]
    public async Task ADownstreamFailure_IsBoundedAndStructured_AgainstARealUnreachableHost()
    {
        await using var bff = BffTestHost.CreateBffWithUnreachableService("ProductsApi", UnreachableAddress);
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Đỏ nếu
        // 500 hay 200.
        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.BadGateway, HttpStatusCode.GatewayTimeout });
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đúng kiểu nội dung.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đạt khi có giới hạn thời
        // gian.
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Detail
        // phải nêu tên dependency.
        Assert.Contains("ProductsApi", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng. Phải có correlationId.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }
}
