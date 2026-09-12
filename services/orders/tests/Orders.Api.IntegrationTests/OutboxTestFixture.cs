using IntegrationTestSupport;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// One real SQL Server + one real RabbitMQ instance shared by the whole outbox verification suite
/// (024-verify-transactional-outbox) — constitution Principle III: real dependencies via
/// Testcontainers, never an in-memory provider, a fake bus, or a mocked broker.
/// </summary>
/// <remarks>
/// Composed from the existing <see cref="SqlServerFixture"/> (this project) and
/// <see cref="RabbitMqFixture"/> (<c>shared/IntegrationTestSupport</c>, 010-testcontainers-integration-tests)
/// rather than a new pair of containers, so the outbox suite pays the two Testcontainers start-up
/// costs once for the whole collection instead of once per test class.
/// </remarks>
public sealed class OutboxTestFixture : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlServer = new();
    private readonly RabbitMqFixture _rabbitMq = new();

    public string SqlConnectionString => _sqlServer.ConnectionString;

    public string RabbitMqConnectionString => _rabbitMq.ConnectionString;

    public Task InitializeAsync() =>
        Task.WhenAll(_sqlServer.InitializeAsync(), _rabbitMq.InitializeAsync());

    public Task DisposeAsync() =>
        Task.WhenAll(_sqlServer.DisposeAsync(), _rabbitMq.DisposeAsync());
}

/// <summary>
/// Binds <see cref="OutboxTestFixture"/> to every test class in the outbox verification suite, so
/// both containers start once for the whole run rather than once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxVerificationCollectionDefinition : ICollectionFixture<OutboxTestFixture>
{
    public const string Name = "outbox-verification";
}
