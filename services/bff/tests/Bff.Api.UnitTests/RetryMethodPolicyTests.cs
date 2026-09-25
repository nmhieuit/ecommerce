using Bff.Api.DownstreamClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// Spec 020 (timeout/retry/circuit breaker) User Story 3 (FR-006; Edge Case 1): 1 lời gọi GHI không
/// bao giờ được resilience pipeline âm thầm retry. Trước tính năng này, predicate retry mặc định của
/// <c>AddStandardResilienceHandler</c> hoàn toàn không xét HTTP method, nên 1 <c>POST /basket/items</c>
/// bị timeout SAU KHI baskets service đã xử lý xong vẫn có thể bị gửi lại — và thêm món 2 lần.
/// </summary>
/// <remarks>
/// Gọi trực tiếp <see cref="BasketsApiClient"/> qua đăng ký DI thật
/// (<see cref="DownstreamClientRegistrationExtensions.AddDownstreamClients"/>), thay primary HTTP
/// handler bằng 1 handler luôn thất bại và tự đếm số lần được gọi — không cần host ASP.NET Core hay
/// map route, vì chính sách đang kiểm tra nằm hoàn toàn trong resilience pipeline gắn vào
/// <c>HttpClient</c> đặt tên. <see cref="BasketsApiClient.GetCurrentBasketAsync"/> (<c>GET</c>) và
/// <see cref="BasketsApiClient.AddItemAsync"/> (<c>POST</c>) đều là lời gọi proxy qua cùng 1 client,
/// cùng 1 cấu hình pipeline — nên so sánh số lần được gọi tách riêng được ràng buộc theo method khỏi
/// mọi thứ khác pipeline làm.
/// </remarks>
public class RetryMethodPolicyTests
{
    /// <summary>
    /// Khớp <c>DownstreamClientRegistrationExtensions.MaxRetryAttempts</c>: 1 lần gọi đầu + 2 lần retry.
    /// </summary>
    private const int ExpectedAttemptsWhenRetried = 3;

    /// <summary>
    /// Kiểm tra: `GET` (đọc giỏ hàng) gặp lỗi tạm thời thì được retry — handler bị gọi đúng 3 lần
    /// (1 + 2 retry).
    /// Lý do phải test: chặn hồi quy — thu hẹp retry theo method không được vô tình tắt luôn retry của
    /// `GET`, vì đọc lại 2 lần không đổi kết quả nghiệp vụ.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-005/FR-006, US3, research.md Decision 5.
    /// </summary>
    [Fact]
    public async Task GetCurrentBasket_IsRetried_OnATransientFailure()
    {
        var handler = new CountingFailingHandler();
        var basketsClient = BuildBasketsClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(
            () => basketsClient.GetCurrentBasketAsync(CancellationToken.None));

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 3 lần gọi thật
        // (1 lần đầu + 2 retry); ít hơn nghĩa là GET đã mất khả năng retry.
        Assert.Equal(ExpectedAttemptsWhenRetried, handler.InvocationCount);
    }

    /// <summary>
    /// Kiểm tra: `POST` (thêm món vào giỏ) gặp lỗi tạm thời thì TUYỆT ĐỐI không được retry — handler
    /// chỉ bị gọi đúng 1 lần.
    /// Lý do: FR-006 — hệ thống không có cơ chế idempotency-key nào, nên 1 POST đã tới server thành
    /// công nhưng mất phản hồi trên đường về mà bị retry sẽ tạo dòng giỏ hàng (hoặc đơn hàng) trùng.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-006, US3, Edge Case 1.
    /// </summary>
    [Fact]
    public async Task AddItem_IsNeverRetried_OnATransientFailure()
    {
        var handler = new CountingFailingHandler();
        var basketsClient = BuildBasketsClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(
            () => basketsClient.AddItemAsync(
                new AddBasketItemCommand(Guid.NewGuid(), Quantity: 1, UnitPrice: 9.99m),
                CancellationToken.None));

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 1 lần gọi thật; lần
        // thứ 2 nghĩa là pipeline đã retry 1 lệnh ghi, đúng rủi ro trùng side effect mà FR-006 cấm.
        Assert.Equal(1, handler.InvocationCount);
    }

    private static BasketsApiClient BuildBasketsClient(HttpMessageHandler primaryHandler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:ProductsApi:BaseUrl"] = "http://products.test",
                ["Services:BasketsApi:BaseUrl"] = "http://baskets.test",
                ["Services:OrdersApi:BaseUrl"] = "http://orders.test",
                ["Services:PartiesApi:BaseUrl"] = "http://parties.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDownstreamClients(configuration);

        // Registered after AddDownstreamClients, exactly as
        // Bff.Api.IntegrationTests/BffTestHost.cs substitutes a downstream's transport — the named
        // client's resilience pipeline (retry, timeout, circuit breaker) stays exactly as
        // production configures it; only the socket underneath is replaced.
        services.AddHttpClient(BasketsApiClient.ServiceName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services.BuildServiceProvider().GetRequiredService<BasketsApiClient>();
    }

    /// <summary>
    /// Làm mọi lời gọi thất bại ngay với 1 exception được phân loại là tạm thời (khớp
    /// <c>FailingTransportHandler</c> của <c>Bff.Api.IntegrationTests/BffTestHost.cs</c>), đồng thời
    /// đếm số lần resilience pipeline thật sự gọi tới nó.
    /// </summary>
    private sealed class CountingFailingHandler : HttpMessageHandler
    {
        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            throw new HttpRequestException("Simulated transient failure.");
        }
    }
}
