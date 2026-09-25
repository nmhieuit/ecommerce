extern alias ProductsApi;

using IntegrationTestSupport;
using Microsoft.Extensions.DependencyInjection;
using ProductsApi::Products.Api.Data;
using ServiceDefaults;
using Tenancy;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Spec 016 (lan truyền correlation ID từ edge đến frontend) US1/US3, research.md Decision 1/7: hop
/// BFF → domain service là nơi DUY NHẤT mà 1 typed <see cref="HttpClient"/> không tự chuyển tiếp gì
/// cả — khác hop gateway → BFF, nơi YARP tự relay header miễn phí
/// (<c>Gateway.Api.IntegrationTests.CorrelationIdPropagationTests</c>). Không có 1 handler outbound
/// mang nó đi, đây chính là nơi correlation ID sinh ở edge sẽ âm thầm dừng lại, và mỗi domain service
/// chỉ tới được qua BFF sẽ tự sinh ID riêng của nó thay vì mang theo ID đã có.
/// </summary>
/// <remarks>
/// Giống hệt <see cref="TenantPropagationTests"/>: khẳng định được đặt ngay trên outbound request,
/// qua 1 recording handler nằm trong pipeline của client — vì đó chính là thứ đang được kiểm tra;
/// domain service phía dưới có "thích" giá trị nhận được hay không là việc của bộ test riêng của
/// chính service đó.
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class CorrelationPropagationTests(DownstreamServicesFixture fixture)
{
    /// <summary>
    /// An unresolved tenant would make Products throw <c>MissingTenantContextException</c>
    /// (contracts/tenant-id-header.md) before ever answering — turning this test's request into one
    /// the resilience pipeline treats as failed and retries, which would multiply
    /// <see cref="OutboundCorrelationIdRecorder.Observed"/> by the retry count instead of leaving it
    /// at one call per logical request. A resolved tenant keeps this suite about correlation ID
    /// propagation only, not an incidental proof of the resilience pipeline's retry budget.
    /// </summary>
    private const string ResolvedTenant = "contoso";

    /// <summary>
    /// Task nguồn: spec 016 (lan truyền correlation ID từ edge đến frontend) — US1/US3, research.md
    /// Decision 1 (FR-003).
    /// </summary>
    [Fact]
    public async Task TheBffsOutboundCall_CarriesTheCorrelationIdTheBffReceived()
    {
        const string supplied = "bff-correlation-propagation-test";

        await using var products = await CreateEmptyProductsServiceAsync("bff-correlation-propagation");

        var recorder = new OutboundCorrelationIdRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);
        request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);

        await client.SendAsync(request);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn — outbound call phải mang đúng 1 giá trị correlation ID.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — id BFF nhận từ request
        // phải khớp đúng id gửi đi tới domain service, không bị rớt hoặc bị đổi.
        Assert.Equal(supplied, Assert.Single(recorder.Observed));
    }

    /// <summary>
    /// Kiểm tra: gửi 10 request đồng thời, mỗi request mang 1 correlation ID riêng — outbound call
    /// tới domain service phải khớp 1-1 với đúng correlation ID của chính request đó, không request
    /// nào bị lẫn ID của request khác.
    /// Lý do: research.md Decision 7 — rủi ro thật nằm ở 1 handler `HttpClient` được pool và tái sử
    /// dụng giữa các request (`IHttpClientFactory`) chụp lại state của 1 request rồi phát lại cho
    /// request khác — cùng lớp lỗi mà chính doc-comment của `TenantPropagationHandler` đã cảnh báo
    /// cho tenant. Đọc `IHttpContextAccessor.HttpContext` tươi mỗi lần gọi `SendAsync` (không capture
    /// lúc khởi tạo) là cơ chế phải ngăn được lỗi đó; test này chứng minh bằng tải đồng thời thật,
    /// không chỉ bằng lý luận đọc mã.
    /// Task nguồn: spec 016 (lan truyền correlation ID từ edge đến frontend) — US3, research.md
    /// Decision 7 (FR-007).
    /// </summary>
    [Fact]
    public async Task TheBffsOutboundCalls_DoNotCrossContaminateCorrelationIds_UnderConcurrentRequests()
    {
        const int concurrentRequests = 10;

        await using var products = await CreateEmptyProductsServiceAsync("bff-correlation-concurrency");

        var recorder = new OutboundCorrelationIdRecorder();
        await using var bff = CreateRecordingBff(products, recorder);

        var sent = Enumerable.Range(0, concurrentRequests)
            .Select(i => $"concurrent-correlation-{i}")
            .ToArray();

        await Task.WhenAll(sent.Select(async correlationId =>
        {
            var client = bff.CreateClient().UseTestBearerToken();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
            request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);
            await client.SendAsync(request);
        }));

        var observed = recorder.Observed;
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — số outbound call quan sát
        // được phải đúng bằng số request đã gửi, không thiếu không thừa.
        Assert.Equal(sent.Length, observed.Count);
        // Mỗi id đã gửi phải được quan sát đúng 1 lần — không id nào thiếu, không id nào bị nhân đôi
        // lên outbound call của 1 request khác (đó chính là biểu hiện của lẫn lộn correlation ID).
        // Assert.Equal(kỳ vọng, thực tế): xanh khi 2 tập hợp (đã sắp xếp) giống hệt nhau, đỏ khi khác.
        Assert.Equal(sent.OrderBy(id => id), observed.OrderBy(id => id));
    }

    private Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program>>
        CreateEmptyProductsServiceAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor(database),
            async dbContext =>
            {
                dbContext.Products.RemoveRange(dbContext.Products);
                await dbContext.SaveChangesAsync();
            });

    /// <summary>
    /// Appends the recorder to the products client's handler pipeline. Registered after the BFF's
    /// own handlers, which puts it innermost — so it observes the request as it finally leaves,
    /// correlation ID header and all, rather than before the propagation handler has run.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateRecordingBff(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program> products,
        OutboundCorrelationIdRecorder recorder) =>
        BffTestHost.CreateBff("ProductsApi", products).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient("ProductsApi")
                    .AddHttpMessageHandler(() => new RecordingHandler(recorder))));

    private sealed class OutboundCorrelationIdRecorder
    {
        private readonly List<string?> _observed = [];

        public IReadOnlyList<string?> Observed
        {
            get
            {
                lock (_observed)
                {
                    return _observed.ToArray();
                }
            }
        }

        public void Record(string? correlationId)
        {
            lock (_observed)
            {
                _observed.Add(correlationId);
            }
        }
    }

    private sealed class RecordingHandler(OutboundCorrelationIdRecorder recorder) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            recorder.Record(
                request.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values)
                    ? values.Single()
                    : null);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
