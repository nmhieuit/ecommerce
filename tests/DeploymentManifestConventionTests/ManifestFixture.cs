using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeploymentManifestConventionTests;

/// <summary>
/// Resolves each service's flat template variables exactly the way
/// deploy/ansible/roles/service_deployment/tasks/main.yml's <c>set_fact</c> step must (merge the
/// group default from defaults/main.yml with any per-service override from services.yml), renders
/// deployment.yaml.j2 with them, and parses the result back into <see cref="DeploymentManifest"/>.
/// </summary>
/// <remarks>
/// This is the one place a change to either YAML file's shape must be echoed — by design, the same
/// way <c>DockerfileReferenceScanner</c> is the one place that knows the Dockerfile COPY-line
/// pattern. A test that fails here because a resolved variable is missing is telling the truth: the
/// template asked for something nothing yet supplies.
/// </remarks>
public static class ManifestFixture
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static IReadOnlyDictionary<string, DeploymentManifest> RenderAll(string repositoryRoot)
    {
        var inventory = ServiceInventory.Load(repositoryRoot);
        var probeDefaults = ProbeDefaults.Load(repositoryRoot);
        var templateContent = File.ReadAllText(ProbeTemplateRenderer.TemplatePath(repositoryRoot));

        var result = new Dictionary<string, DeploymentManifest>();
        foreach (var (serviceName, entry) in inventory)
        {
            var variables = ResolveVariables(serviceName, entry, probeDefaults);
            var rendered = ProbeTemplateRenderer.Render(templateContent, variables);
            result[serviceName] = YamlDeserializer.Deserialize<DeploymentManifest>(rendered);
        }

        return result;
    }

    private static Dictionary<string, string> ResolveVariables(
        string serviceName,
        ServiceInventoryEntry entry,
        IReadOnlyDictionary<string, ProbeGroupDefaults> probeDefaults)
    {
        var group = entry.DependsOnDatabase ? ProbeDefaultGroups.DbBacked : ProbeDefaultGroups.Stateless;
        var groupDefaults = probeDefaults[group];

        // Contract rule 4 (service-deployment-vars.md): an override, if present, replaces all four
        // fields for BOTH probes of that service — there is no partial or per-probe override.
        var readiness = entry.ProbeTimingOverrides ?? groupDefaults.Readiness;
        var liveness = entry.ProbeTimingOverrides ?? groupDefaults.Liveness;

        return new Dictionary<string, string>
        {
            ["service_name"] = serviceName,
            ["image"] = $"{serviceName}:latest",
            ["container_port"] = entry.ContainerPort.ToString(CultureInfo.InvariantCulture),
            ["liveness_path"] = "/health/live",
            ["readiness_path"] = "/health/ready",
            ["readiness_initial_delay_seconds"] = readiness.InitialDelaySeconds.ToString(CultureInfo.InvariantCulture),
            ["readiness_period_seconds"] = readiness.PeriodSeconds.ToString(CultureInfo.InvariantCulture),
            ["readiness_timeout_seconds"] = readiness.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["readiness_failure_threshold"] = readiness.FailureThreshold.ToString(CultureInfo.InvariantCulture),
            ["liveness_initial_delay_seconds"] = liveness.InitialDelaySeconds.ToString(CultureInfo.InvariantCulture),
            ["liveness_period_seconds"] = liveness.PeriodSeconds.ToString(CultureInfo.InvariantCulture),
            ["liveness_timeout_seconds"] = liveness.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["liveness_failure_threshold"] = liveness.FailureThreshold.ToString(CultureInfo.InvariantCulture),
        };
    }
}
