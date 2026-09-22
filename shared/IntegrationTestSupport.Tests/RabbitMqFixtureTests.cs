using IntegrationTestSupport;
using RabbitMQ.Client;

namespace IntegrationTestSupport.Tests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-004, FR-006, FR-008: proves
/// <see cref="RabbitMqFixture"/> starts a real RabbitMQ container that a real client can connect
/// to, and that killing the container mid-test fails the affected test within a bounded time
/// instead of hanging (spec SC-004).
/// </summary>
public class RabbitMqFixtureTests(RabbitMqFixture rabbitMq) : IClassFixture<RabbitMqFixture>
{
    /// <summary>
    /// Kiểm tra: mở 1 kết nối `RabbitMQ.Client` thật tới container do `RabbitMqFixture` dựng — kết
    /// nối phải mở thành công.
    /// Lý do: chứng minh fixture dùng chung khởi động được broker RabbitMQ thật và kết nối được,
    /// trước khi bất kỳ service nào cần publish/consume qua nó.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T018/T019, US3 (FR-004, FR-006).
    /// </summary>
    [Fact]
    public async Task RabbitMqFixture_Connects_ToARealBroker()
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMq.ConnectionString) };

        await using var connection = await factory.CreateConnectionAsync();

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Điều kiện: kết nối đang mở
        // (`IsOpen`); đỏ khi container không khởi động được hoặc kết nối bị từ chối.
        Assert.True(connection.IsOpen);
    }

    /// <summary>
    /// Kiểm tra: mở kết nối, chủ động "giết" container broker giữa lúc đang có thao tác đang chờ
    /// (`QueueDeclareAsync`) — thao tác đó phải thất bại trong vòng 30 giây, không treo vô hạn.
    /// Lý do: chứng minh FR-008/SC-004 — khi broker chết giữa lúc test đang chạy, chính bài test
    /// (không chỉ code sản xuất) không được treo; research.md Decision 5 dùng timeout ngắn phía
    /// client cộng 1 giới hạn cứng để chính bài test không thể treo dù client có mặc định rộng rãi
    /// tới đâu.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T020/T021, US3 (FR-008, SC-004).
    /// </summary>
    [Fact]
    public async Task RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest()
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(rabbitMq.ConnectionString),
            ContinuationTimeout = TimeSpan.FromSeconds(5),
            RequestedHeartbeat = TimeSpan.FromSeconds(5),
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await rabbitMq.KillBrokerAsync();

        var operation = channel.QueueDeclareAsync(
            queue: "010-smoke-test-queue-after-kill",
            durable: false,
            exclusive: false,
            autoDelete: true);

        var bounded = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(30)));

        // Assert.Same(kỳ vọng, thực tế): xanh khi 2 tham chiếu CÙNG 1 đối tượng. `bounded` là kết
        // quả của Task.WhenAny(operation, hẹn giờ 30s) — phải chính là `operation` đã xong (không
        // phải bộ đếm giờ), tức thao tác đã kết thúc TRƯỚC mốc 30 giây; đỏ nếu `bounded` là task hẹn
        // giờ, nghĩa là thao tác đã treo quá 30 giây.
        Assert.Same(operation, bounded);
        // Assert.ThrowsAnyAsync(đoạn mã): xanh khi đoạn mã ném BẤT KỲ ngoại lệ nào. Broker đã chết
        // nên thao tác phải ném lỗi (mất kết nối), không được âm thầm coi như thành công.
        await Assert.ThrowsAnyAsync<Exception>(() => operation);
    }
}
