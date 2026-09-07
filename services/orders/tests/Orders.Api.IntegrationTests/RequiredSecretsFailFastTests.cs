using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// specs/018-cluster-secret-store FR-007 and User Story 2's Independent Test: a service that
/// never received its required secret from the cluster secret store must fail to start, with a
/// clear reason, rather than start in an undefined state. The committed, non-Development
/// appsettings.json deliberately still carries a host/database-only connection string with no
/// credential (contracts/service-configuration-contract.md rule 2) — this is exactly the shape a
/// real cluster deployment falls back to if the ExternalSecret/Secret was never wired up, so the
/// test does not need to remove the key, only decline to supply a credential for it, matching
/// production reality rather than an artificial "config entirely absent" case.
/// </summary>
public class RequiredSecretsFailFastTests
{
    [Fact]
    public async Task HostFailsToStart_WhenOrdersDbConnectionStringHasNoCredential()
    {
        await using var factory = CreateFactoryWithoutCredential();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.True(ExceptionChainMentions(exception, "ConnectionStrings:OrdersDb"),
            $"Expected the startup failure to name the missing secret 'ConnectionStrings:OrdersDb'. Actual exception chain: {Describe(exception)}");
    }

    private static bool ExceptionChainMentions(Exception? exception, string text)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (ExceptionChainMentions(inner, text))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string Describe(Exception? exception) =>
        exception is null ? "<none>" : string.Join(" -> ", EnumerateMessages(exception));

    private static IEnumerable<string> EnumerateMessages(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return $"{current.GetType().Name}: {current.Message}";
        }
    }

    private static WebApplicationFactory<Program> CreateFactoryWithoutCredential()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Matches docker-compose.yml's own Production stack: no ASPNETCORE_ENVIRONMENT is set,
            // so a service that never got its cluster secret runs on appsettings.json's base
            // (credential-less) ConnectionStrings:OrdersDb value — never appsettings.Development.json.
            builder.UseEnvironment("Production");
        });
    }
}
