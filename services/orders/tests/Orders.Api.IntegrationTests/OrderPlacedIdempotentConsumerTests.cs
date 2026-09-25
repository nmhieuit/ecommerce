using EventContracts;
using IntegrationTestSupport;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Orders.Api.IntegrationTests.Support;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Spec 024 (xác minh outbox pattern giao dịch) — Bất biến 4 của
/// `contracts/outbox-guarantees-contract.md` (FR-005, User Story 3): một consumer nhận cùng một
/// <see cref="OrderPlacedV1"/> (cùng message id) hai lần chỉ thực sự xử lý đúng 1 lần, dùng
/// `InboxState` thật của MassTransit (`research.md` Quyết định 3) — không phải dedupe tự viết. Dùng
/// SQL Server + RabbitMQ thật qua Testcontainers (`OutboxTestFixture`).
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedIdempotentConsumerTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: publish cùng 1 `OrderPlacedV1` hai lần với cùng `MessageId` vào broker; consumer xác
    /// minh (chỉ tồn tại trong test, dùng Consumer Outbox/`InboxState` thật) phải nhận được sự kiện,
    /// và sau 3 giây chờ thêm, thân consumer chỉ chạy đúng 1 lần cho `EventId` đó.
    /// Lý do (phải test): FR-005/US3-KB1/SC-003 — "gửi ít nhất một lần" của outbox chỉ an toàn khi
    /// phía nhận không tạo tác dụng phụ nhân đôi.
    /// Lưu ý: hai lần publish nối tiếp nhau (không đồng thời — US3-KB2 không có test); không có test
    /// cho 2 sự kiện khác nhau của cùng 1 đơn hàng (US3-KB3); test khẳng định đếm chạy của thân
    /// consumer, không đọc trực tiếp bảng `InboxState`. Test này vẫn xanh khi gỡ `UseBusOutbox()` vì
    /// nó tự publish qua `IPublishEndpoint`, không đi qua endpoint `POST /orders`.
    /// Task nguồn: spec 024 (xác minh outbox pattern giao dịch) — T019/T017/T018, Bất biến 4.
    /// </summary>
    [Fact]
    public async Task Consumer_ProcessesTheSameRedeliveredMessage_ExactlyOnce()
    {
        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await using var verificationHost = await VerificationConsumerHost.StartAsync(
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.SqlConnectionString)
            {
                InitialCatalog = "orders-outbox-verification-idempotency",
            }.ConnectionString,
            fixture.RabbitMqConnectionString);

        await using var factory = await CreateFactoryAsync("orders-idempotency");

        var placedEvent = new OrderPlacedV1(
            eventId,
            DateTime.UtcNow,
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid().ToString("n"),
            12.50m,
            [new OrderLineV1(Notebook, 1, 12.50m)]);

        // Hai lần publish riêng biệt mang CÙNG MessageId — đúng khoá mà InboxState dùng để khử trùng
        // (broker gửi lại/retry thật vẫn giữ nguyên message id; EventId là trường nghiệp vụ nằm
        // trong payload, không phải id của phong bì truyền tải mà InboxState dùng làm khoá).
        await PublishAsync(factory, placedEvent, messageId);
        await PublishAsync(factory, placedEvent, messageId);

        var observed = await WaitForAtLeastOneDeliveryAsync(eventId, TimeSpan.FromSeconds(30));
        // Assert.True(điều kiện, thông báo): xanh khi consumer nhận được sự kiện trong 30 giây; đỏ (đúng
        // thông báo này) khi không có gì tới — publish/broker hỏng, nên kiểm tra đếm bên dưới vô nghĩa.
        Assert.True(observed, "The verification consumer never observed the published event.");

        // Khoảng chờ đủ rộng: nếu cơ chế khử trùng hỏng, lần giao thứ hai cũng sẽ rơi vào khoảng này
        // (cùng broker, cùng tiến trình, không có phân mảnh mạng nào phải chờ).
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert.Equal(1, số lần chạy): xanh khi thân consumer chạy đúng 1 lần cho EventId (bản trùng
        // bị InboxState chặn); đỏ khi >= 2 (tác dụng phụ bị nhân đôi — dedup không hoạt động).
        Assert.Equal(1, OrderPlacedVerificationConsumer.ProcessedCounts[eventId]);
    }

    private static async Task PublishAsync(WebApplicationFactory<Program> factory, OrderPlacedV1 evt, Guid messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await publishEndpoint.Publish(evt, context => context.MessageId = messageId);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<bool> WaitForAtLeastOneDeliveryAsync(Guid eventId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (OrderPlacedVerificationConsumer.ProcessedCounts.ContainsKey(eventId))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return false;
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database)
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
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        return factory;
    }
}
