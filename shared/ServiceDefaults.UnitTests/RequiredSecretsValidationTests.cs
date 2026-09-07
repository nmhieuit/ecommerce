using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ServiceDefaults;

namespace ServiceDefaults.UnitTests;

/// <summary>
/// specs/018-cluster-secret-store FR-007 and Test Scenario: a service that is missing a secret it
/// declared as required must fail fast at startup with a clear, structured reason — never start in
/// an undefined state and never leak the secret's value. data-model.md §1 (RequiredSecret) and
/// research.md #3 give this exactly two outcomes — validation passes, or it fails naming the
/// missing secret(s) — and this suite is what keeps a third "starts anyway" outcome from quietly
/// appearing.
/// </summary>
public class RequiredSecretsValidationTests
{
    private static IConfiguration ConfigurationWith(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    [Fact]
    public void Validate_Succeeds_WhenEveryRequiredSecretHasAValue()
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", "Server=orders-db;User Id=sa;Password=Sup3r$ecret;..."));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenARequiredSecretIsMissing()
    {
        var configuration = ConfigurationWith();
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
    }

    /// <summary>
    /// Blank is not a value, the same rule Tenancy.UnitTests enforces for TenantContext — an env
    /// var accidentally set to an empty string must not be treated as "supplied".
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Validate_Fails_WhenARequiredSecretIsBlank(string blank)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", blank));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void FailureMessage_NamesTheMissingSecret_ButNeverASecretValue()
    {
        var configuration = ConfigurationWith(("ConnectionStrings:PartiesDb", "Server=parties-db;Password=TopSecret123;..."));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions
        {
            Secrets =
            [
                RequiredSecret.ConnectionString("PartiesDb"),
                RequiredSecret.ConnectionString("MissingDb"),
            ],
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:MissingDb", result.FailureMessage);
        Assert.DoesNotContain("TopSecret123", result.FailureMessage);
    }

    [Fact]
    public void Validate_Succeeds_WhenNoSecretsAreDeclared()
    {
        var configuration = ConfigurationWith();
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions();

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ConnectionString_BuildsANameMatchingTheStandardConnectionStringsConfigurationKey()
    {
        var secret = RequiredSecret.ConnectionString("OrdersDb");

        Assert.Equal("ConnectionStrings:OrdersDb", secret.Name);
    }

    /// <summary>
    /// Discovered while implementing tasks.md T019 (US2 fail-fast integration test): the
    /// committed, non-Development appsettings.json intentionally still carries a non-blank
    /// host/database-only connection string (contracts/service-configuration-contract.md rule 2 —
    /// credentials must be absent from it, not the whole key). A plain blank/non-blank check on
    /// that value would never fail, even when the cluster never injected a credential — the exact
    /// case FR-007 exists to catch. RequiredSecret.ConnectionString must resolve to "missing"
    /// (null) when the string has no credential component, not just when it is entirely absent.
    /// </summary>
    [Theory]
    [InlineData("Server=orders-db;Database=orders;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;User Id=sa;TrustServerCertificate=True")]
    public void ConnectionString_TreatsAHostOnlyValueWithNoCredential_AsMissing(string credentialLessConnectionString)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", credentialLessConnectionString));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("Server=orders-db;Database=orders;User Id=sa;Password=Sup3r$ecret;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;Pwd=Sup3r$ecret;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;Integrated Security=true;TrustServerCertificate=True")]
    public void ConnectionString_Succeeds_WhenACredentialComponentIsPresent(string credentialedConnectionString)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", credentialedConnectionString));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }
}
