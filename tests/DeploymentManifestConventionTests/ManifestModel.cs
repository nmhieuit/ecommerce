namespace DeploymentManifestConventionTests;

/// <summary>
/// Minimal projection of a Kubernetes Deployment — only the fields this suite asserts on. YAML keys
/// are camelCase (contracts/probe-manifest-shape.md); the deserializer in
/// <see cref="ManifestFixture"/> is configured with YamlDotNet's camel-case naming convention so
/// these PascalCase properties map without attributes.
/// </summary>
public sealed class DeploymentManifest
{
    public string? ApiVersion { get; set; }

    public string? Kind { get; set; }

    public DeploymentSpec? Spec { get; set; }
}

public sealed class DeploymentSpec
{
    public DeploymentStrategy? Strategy { get; set; }

    public PodTemplateSpec? Template { get; set; }
}

public sealed class DeploymentStrategy
{
    public string? Type { get; set; }

    public RollingUpdateSpec? RollingUpdate { get; set; }
}

public sealed class RollingUpdateSpec
{
    public int MaxUnavailable { get; set; }

    public int MaxSurge { get; set; }
}

public sealed class PodTemplateSpec
{
    public PodSpec? Spec { get; set; }
}

public sealed class PodSpec
{
    public List<ContainerSpec> Containers { get; set; } = [];
}

public sealed class ContainerSpec
{
    public string? Name { get; set; }

    public string? Image { get; set; }

    public List<ContainerPortSpec> Ports { get; set; } = [];

    public ProbeSpec? LivenessProbe { get; set; }

    public ProbeSpec? ReadinessProbe { get; set; }
}

public sealed class ContainerPortSpec
{
    public int ContainerPort { get; set; }
}

public sealed class ProbeSpec
{
    public HttpGetActionSpec? HttpGet { get; set; }

    public int InitialDelaySeconds { get; set; }

    public int PeriodSeconds { get; set; }

    public int TimeoutSeconds { get; set; }

    public int FailureThreshold { get; set; }
}

public sealed class HttpGetActionSpec
{
    public string? Path { get; set; }

    public int Port { get; set; }
}
