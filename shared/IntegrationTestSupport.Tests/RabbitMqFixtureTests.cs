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
    [Fact]
    public async Task RabbitMqFixture_Connects_ToARealBroker()
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMq.ConnectionString) };

        await using var connection = await factory.CreateConnectionAsync();

        Assert.True(connection.IsOpen);
    }

    /// <summary>
    /// research.md Decision 5: a short client-side timeout plus a bounded assertion, so the test
    /// itself cannot hang past SC-004's 30-second bound even if a client default is more generous.
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

        Assert.Same(operation, bounded);
        await Assert.ThrowsAnyAsync<Exception>(() => operation);
    }
}
