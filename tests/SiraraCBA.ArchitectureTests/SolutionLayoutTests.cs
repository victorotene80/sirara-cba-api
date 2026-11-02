namespace SiraraCBA.ArchitectureTests;

/// <summary>The solution layout itself is part of the architecture (architecture.md §2).</summary>
public sealed class SolutionLayoutTests
{
    [Fact]
    public void EveryProjectLivesUnderSrcOrTests()
    {
        SolutionTree.Projects
            .Where(project => !project.RelativePath.StartsWith("src/", StringComparison.Ordinal)
                              && !project.RelativePath.StartsWith("tests/", StringComparison.Ordinal))
            .Select(project => project.RelativePath)
            .ShouldBeEmpty();
    }

    [Fact]
    public void OnlyTheApiHostIsAWebProject()
    {
        // Scans the whole repository, not just src/. Scanning src/ alone let a test
        // project take the Web SDK while this test stayed green — the rule passed
        // because of where it looked, not because of what was true.
        List<string> webProjects = SolutionTree.Projects
            .Where(project => File.ReadAllText(Path.Combine(SolutionTree.Root, project.RelativePath))
                .Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal))
            .Select(project => project.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        webProjects.ShouldBe(
            ["SiraraCBA.Api"],
            "One deployable, one composition root. A suite that boots the host takes "
            + "a FrameworkReference, not the Web SDK (architecture.md §2, §7).");
    }

    [Fact]
    public void PackageVersionsAreManagedCentrally()
    {
        List<string> violations = SolutionTree.Projects
            .Where(project => File.ReadAllText(Path.Combine(SolutionTree.Root, project.RelativePath))
                .Contains("PackageReference Include=\"", StringComparison.Ordinal)
                && File.ReadAllText(Path.Combine(SolutionTree.Root, project.RelativePath))
                    .Contains("Version=", StringComparison.Ordinal))
            .Select(project => project.Name)
            .ToList();

        violations.ShouldBeEmpty(
            "A version string appears exactly once in the solution, in "
            + "Directory.Packages.props (coding-standards.md, Solution hygiene).");
    }

    [Fact]
    public void NoSecretsAreCommitted()
    {
        string[] markers = ["Password=", "password=", "ApiKey", "api_key", "BEGIN RSA PRIVATE KEY", "BEGIN PRIVATE KEY"];

        List<string> violations = Directory
            .EnumerateFiles(Path.Combine(SolutionTree.Root, "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (Path: path, Number: index + 1, Text: line))
                .Where(line => markers.Any(marker => line.Text.Contains(marker, StringComparison.Ordinal))))
            .Select(line => $"{Path.GetFileName(line.Path)}:{line.Number}")
            .ToList();

        violations.ShouldBeEmpty("No secret, connection string or key in source or committed config.");
    }
}
