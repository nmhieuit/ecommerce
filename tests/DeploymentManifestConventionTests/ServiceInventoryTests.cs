namespace DeploymentManifestConventionTests;

/// <summary>
/// Spec SC-001: 100% of running services declare both probes. That claim is only meaningful if the
/// inventory the role deploys from actually lists every service — a scan that silently examines
/// fewer than 7 would report "all pass" while missing one entirely, the same trap
/// <c>ContainerConventionTests.TheScan_Examined_EveryService</c> guards against for Dockerfiles.
/// </summary>
public class ServiceInventoryTests
{
    /// <summary>Every service that has a Deployment, and therefore must be in the inventory.</summary>
    private static readonly string[] ExpectedServices =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    [Fact]
    public void Inventory_ListsExactlyTheSevenExpectedServices()
    {
        var inventory = ServiceInventory.Load(ProbeTemplateRenderer.LocateRepositoryRoot());

        Assert.Equal(ExpectedServices.OrderBy(s => s), inventory.Keys.OrderBy(s => s));
    }

    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresContainerPortAndDependsOnDatabase(string serviceName)
    {
        var inventory = ServiceInventory.Load(ProbeTemplateRenderer.LocateRepositoryRoot());

        Assert.True(inventory.ContainsKey(serviceName), $"'{serviceName}' is missing from services.yml.");
        var entry = inventory[serviceName];

        Assert.True(entry.ContainerPort > 0, $"'{serviceName}' must declare a positive container_port.");
        // depends_on_database has no implicit default (contracts/service-deployment-vars.md rule 3) —
        // ServiceInventory.Load throws if the key is absent, so reaching this line already proves it
        // is present; this assertion documents the two services that opt out of it.
        if (serviceName is "gateway" or "bff")
        {
            Assert.False(entry.DependsOnDatabase);
        }
        else
        {
            Assert.True(entry.DependsOnDatabase);
        }
    }
}
