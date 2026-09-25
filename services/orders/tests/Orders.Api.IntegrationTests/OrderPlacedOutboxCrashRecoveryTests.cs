using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Orders.Api.IntegrationTests.Support;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Spec 024 (xác minh outbox pattern giao dịch) — Bất biến 3 của
/// `contracts/outbox-guarantees-contract.md` (FR-003/FR-004, User Story 2): nếu tiến trình sập ngay
/// sau khi transaction tạo đơn commit nhưng trước khi outbox delivery service kịp gửi, khởi động lại
/// vẫn tự động phát sự kiện đó ra ngoài — kịch bản 2-host của `research.md` Quyết định 4. Dùng SQL
/// Server + RabbitMQ thật qua Testcontainers (`OutboxTestFixture`).
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedOutboxCrashRecoveryTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: Host A (chu kỳ quét outbox 120 giây) nhận `POST /orders` → `201`, xác nhận hàng outbox
    /// của đơn đó đã commit, rồi bị huỷ ngay (mô phỏng sập tiến trình). Host B mới (cùng CSDL, chu kỳ
    /// quét 1 giây) khởi động; consumer xác minh độc lập phải nhận được thêm 1 sự kiện trong vòng 30
    /// giây mà không cần thao tác thủ công nào khác.
    /// Lý do (phải test): FR-003/FR-004/US2-KB1/SC-002 — đây là giá trị cốt lõi của outbox: sự kiện đã
    /// commit không được mất dù tiến trình chết đúng "điểm mù" giữa ghi dữ liệu và gửi message.
    /// Lưu ý: kiểm chứng bằng mutation — bỏ Host B thì test đỏ (Host A không tự gửi khi bị huỷ); nhưng
    /// gán cứng chu kỳ quét 1 giây trong Program.cs (bỏ qua cấu hình `Outbox:QueryDelaySeconds`) test
    /// vẫn xanh, và test không khẳng định "chưa gửi trước khi Host B khởi động" (xem QA_Debt mục 024).
    /// Chỉ chứng minh 1 lần khởi động lại (US2-KB3 — nhiều lần khởi động lại — không có test).
    /// Task nguồn: spec 024 (xác minh outbox pattern giao dịch) — T015/T016, Bất biến 3.
    /// </summary>
    [Fact]
    public async Task OutboxMessage_CommittedButUnsentWhenTheProcessStops_StillGetsPublished_AfterRestart()
    {
        await using var verificationHost = await VerificationConsumerHost.StartAsync(
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.SqlConnectionString)
            {
                InitialCatalog = "orders-outbox-verification-crash-recovery",
            }.ConnectionString,
            fixture.RabbitMqConnectionString);

        var deliveredCountBeforeCrash = OrderPlacedVerificationConsumer.ProcessedCounts.Count;

        var database = "orders-outbox-crash-recovery";

        // Host A ("trước khi sập"): outbox delivery service của nó được cấu hình quét mỗi 2 phút —
        // dài hơn nhiều so với thời gian test cần để tạo đơn rồi huỷ host — nên commit bên dưới chắc
        // chắn kết thúc khi OutboxMessage vẫn chưa được gửi lúc Host A bị huỷ ngay sau đó.
        Guid orderId;
        await using (var hostA = await CreateFactoryAsync(database, queryDelaySeconds: 120))
        {
            var client = CreateClient(hostA);

            var response = await client.PostAsJsonAsync("/orders", new
            {
                items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
            });

            // Assert.Equal(mong đợi, thực tế): xanh khi Host A nhận đơn và trả `201`; đỏ khi mã khác —
            // không có đơn để mô phỏng sập.
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
            // Assert.NotNull: xanh khi đọc được đơn hàng vừa tạo; đỏ khi body sai hình dạng.
            Assert.NotNull(order);
            orderId = order!.Id;

            // Xác nhận trực tiếp tiền đề, giống cách OrderPlacedOutboxAtomicityTests làm: hàng outbox
            // đã commit, còn nằm trong CSDL mà Host A để lại.
            using var verifyScope = hostA.Services.CreateScope();
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var outboxCount = await dbContext.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
                .CountAsync(row => row.Body.Contains(orderId.ToString()));
            // Assert.Equal(1, số hàng): xanh khi đúng 1 hàng outbox chứa id đơn này còn trong CSDL (đã
            // commit, chưa bị giao đi và xoá); đỏ khi 0 (không có outbox — ví dụ gỡ `UseBusOutbox()`
            // → "Expected: 1, Actual: 0") hoặc >1 (bị ghi trùng).
            Assert.Equal(1, outboxCount);
        } // `await using` huỷ Host A tại đây — chính là lần "sập" được mô phỏng.

        // Host B ("khởi động lại"): host hoàn toàn mới, cùng CSDL, chu kỳ quét ngắn — đại diện cho
        // service chạy lại sau khi tiến trình ở trên đã sập.
        await using var hostB = await CreateFactoryAsync(database, queryDelaySeconds: 1);

        // Chạm vào `.Services` là lần đầu Host B (và outbox delivery service của nó) được khởi động.
        using (hostB.Services.CreateScope())
        {
        }

        var delivered = await WaitUntilAsync(
            () => OrderPlacedVerificationConsumer.ProcessedCounts.Count > deliveredCountBeforeCrash,
            TimeSpan.FromSeconds(30));

        // Assert.True(điều kiện, thông báo): xanh khi trong 30 giây consumer xác minh nhận thêm ít nhất
        // 1 sự kiện sau khi Host B khởi động (outbox relay tự gửi bản ghi còn sót); đỏ (đúng thông
        // báo này) khi không có sự kiện nào tới — tức sự kiện đã commit bị mất qua lần "sập".
        Assert.True(
            delivered,
            "Host B never delivered the OutboxMessage Host A committed but did not send before it stopped.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return condition();
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database, int queryDelaySeconds)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            fixture.SqlConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = connectionString,
                    ["ConnectionStrings:RabbitMq"] = fixture.RabbitMqConnectionString,
                    ["Outbox:QueryDelaySeconds"] = queryDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total, string TenantId);
}
