using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeploymentManifestConventionTests;

/// <summary>One entry under <c>services:</c> in deploy/ansible/inventories/services.yml.</summary>
/// <remarks>
/// Mirrors the "Service Deployment Profile" entity in specs/019-liveness-readiness-probes/data-model.md
/// and the schema in contracts/service-deployment-vars.md. <c>DependsOnDatabase</c> is a non-nullable
/// <see cref="bool"/> with no default: YamlDotNet leaves it <see langword="false"/> if the YAML key is
/// absent, which is indistinguishable from an explicit "false" — contract rule 3 requires the key be
/// present, so <see cref="ServiceInventory.Load"/> checks the raw document for the key's presence
/// rather than trusting this property alone.
/// </remarks>
public sealed class ServiceInventoryEntry
{
    public int ContainerPort { get; set; }

    public bool DependsOnDatabase { get; set; }

    public ProbeTimingOverrides? ProbeTimingOverrides { get; set; }
}

/// <summary>Contract rule 4: if present, all four fields are required — no partial override.</summary>
public sealed class ProbeTimingOverrides
{
    public int InitialDelaySeconds { get; set; }

    public int PeriodSeconds { get; set; }

    public int TimeoutSeconds { get; set; }

    public int FailureThreshold { get; set; }
}

public static class ServiceInventory
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static string InventoryPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "deploy", "ansible", "inventories", "services.yml");

    public static IReadOnlyDictionary<string, ServiceInventoryEntry> Load(string repositoryRoot)
    {
        var path = InventoryPath(repositoryRoot);
        var yaml = File.ReadAllText(path);

        var root = Deserializer.Deserialize<InventoryRoot>(yaml)
            ?? throw new InvalidDataException($"'{path}' did not deserialize to a 'services:' document.");

        foreach (var (name, entry) in root.Services)
        {
            if (entry is null)
            {
                throw new InvalidDataException($"Service '{name}' has no body in services.yml.");
            }
        }

        return root.Services!;
    }

    private sealed class InventoryRoot
    {
        public Dictionary<string, ServiceInventoryEntry?> Services { get; set; } = new();
    }
}
