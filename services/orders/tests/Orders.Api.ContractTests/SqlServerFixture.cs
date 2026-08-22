using Testcontainers.MsSql;

namespace Orders.Api.ContractTests;

/// <summary>
/// One real SQL Server instance for the whole contract suite — constitution Principle III:
/// real dependencies via Testcontainers, never an in-memory provider or a hand-rolled fake.
/// Shared at class scope so the suite pays the container start-up cost once instead of per test.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
