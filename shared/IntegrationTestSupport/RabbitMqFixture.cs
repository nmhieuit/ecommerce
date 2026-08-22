using Testcontainers.RabbitMq;
using Xunit;

namespace IntegrationTestSupport;

/// <summary>
/// One real RabbitMQ instance for the test collection that references it — constitution Principle
/// III: real dependencies via Testcontainers, never an in-memory provider or a hand-rolled fake.
/// Matches the shape of the existing per-service <c>SqlServerFixture</c>, but lives here so any
/// future service's integration test project can reference it without duplicating it
/// (010-testcontainers-integration-tests research.md Decision 3).
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3.13-management").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Stops the broker mid-test to simulate a real outage (research.md Decision 5). Not part of
    /// the contract a future service consumer depends on — it exists solely for this feature's own
    /// <c>RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest</c> test (data-model.md).
    /// </summary>
    public Task KillBrokerAsync() => _container.StopAsync();
}
