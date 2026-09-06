using System.Text.RegularExpressions;

namespace DeploymentManifestConventionTests;

/// <summary>
/// Renders <c>deployment.yaml.j2</c> the same way <c>ContainerConventionTests</c> reads
/// Dockerfiles: as plain text, with no external tool invoked. Ansible is not installed to run this
/// suite (019-liveness-readiness-probes research.md Decision 3 — Ansible itself does not run on
/// Windows, only on the Linux/WSL/CI hosts that actually apply the manifest).
/// </summary>
/// <remarks>
/// The template intentionally carries no Jinja2 filter or nested-lookup expression — every
/// placeholder is a bare <c>{{ expression }}</c> whose value the caller already resolved (exactly
/// what <c>tasks/main.yml</c>'s <c>set_fact</c> step does before templating in the real role). This
/// renderer therefore only needs literal substitution to reproduce real Jinja2 output for this
/// template, not a Jinja2 engine.
/// </remarks>
public static class ProbeTemplateRenderer
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{\s*(?<expr>[^}]+?)\s*\}\}", RegexOptions.Compiled);

    /// <summary>The file that marks the repository root — same marker as ContainerConventionTests.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// Repository root, found by walking up from this assembly's location to the marker file.
    /// Deliberately not a ".git" directory check: a git worktree's ".git" is a file, not a
    /// directory, and this suite must resolve correctly whether it runs from the main checkout or a
    /// worktree.
    /// </summary>
    public static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{RepositoryRootMarker}' walking up from '{AppContext.BaseDirectory}'.");
    }

    public static string TemplatePath(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "deploy", "ansible", "roles", "service_deployment", "templates", "deployment.yaml.j2");

    /// <summary>
    /// Renders the template with the given resolved variables. Throws with the missing
    /// expression's exact text if the template references something the caller did not supply —
    /// that failure is the point: it is what makes a test fail for the right reason before the
    /// template exists at all, and again if a future edit adds a placeholder no test resolves.
    /// </summary>
    public static string Render(string templateContent, IReadOnlyDictionary<string, string> resolvedVariables) =>
        PlaceholderPattern.Replace(templateContent, match =>
        {
            var expression = match.Groups["expr"].Value;
            return resolvedVariables.TryGetValue(expression, out var value)
                ? value
                : throw new KeyNotFoundException(
                    $"Template references '{{{{ {expression} }}}}' but no resolved value was supplied for it.");
        });
}
