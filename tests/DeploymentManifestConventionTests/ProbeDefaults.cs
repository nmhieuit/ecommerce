using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeploymentManifestConventionTests;

/// <summary>The two probe-timing groups research.md Decision 2 defines, keyed by group name.</summary>
public static class ProbeDefaultGroups
{
    public const string DbBacked = "db_backed";
    public const string Stateless = "stateless";
}

public sealed class ProbeGroupDefaults
{
    public ProbeTimingOverrides Readiness { get; set; } = new();

    public ProbeTimingOverrides Liveness { get; set; } = new();
}

/// <summary>Loads deploy/ansible/roles/service_deployment/defaults/main.yml.</summary>
public static class ProbeDefaults
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static string DefaultsPath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "deploy", "ansible", "roles", "service_deployment", "defaults", "main.yml");

    public static IReadOnlyDictionary<string, ProbeGroupDefaults> Load(string repositoryRoot)
    {
        var path = DefaultsPath(repositoryRoot);
        var yaml = File.ReadAllText(path);

        var root = Deserializer.Deserialize<DefaultsRoot>(yaml);
        return root?.ProbeDefaults
            ?? throw new InvalidDataException($"'{path}' did not deserialize to a 'probe_defaults:' document.");
    }

    private sealed class DefaultsRoot
    {
        public Dictionary<string, ProbeGroupDefaults> ProbeDefaults { get; set; } = new();
    }
}
