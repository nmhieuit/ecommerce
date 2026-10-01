using Testcontainers.MsSql;
using Xunit;

namespace IntegrationTestSupport;

/// <summary>
/// One real SQL Server instance for the test class or collection that references it — constitution
/// Principle III: real dependencies via Testcontainers, never an in-memory provider or a hand-rolled
/// fake. Shared at class scope so a suite pays the container start-up cost once instead of per test.
/// Consolidates the per-service copies that 010-testcontainers-integration-tests research.md
/// Decision 3 left as a follow-up; same shape as <see cref="RedisFixture"/> and
/// <see cref="RabbitMqFixture"/>.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
