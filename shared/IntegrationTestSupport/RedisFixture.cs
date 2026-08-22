using Testcontainers.Redis;
using Xunit;

namespace IntegrationTestSupport;

/// <summary>
/// One real Redis instance for the test collection that references it — constitution Principle
/// III: real dependencies via Testcontainers, never an in-memory provider or a hand-rolled fake.
/// Matches the shape of the existing per-service <c>SqlServerFixture</c>, but lives here so any
/// future service's integration test project can reference it without duplicating it
/// (010-testcontainers-integration-tests research.md Decision 3).
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7.4").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
