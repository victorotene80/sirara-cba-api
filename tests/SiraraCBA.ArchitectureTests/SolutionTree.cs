using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SiraraCBA.ArchitectureTests;

/// <summary>
/// The solution's project graph and source tree, read from disk.
/// </summary>
/// <remarks>
/// Read from disk rather than from loaded assemblies so that a rule is checked
/// against what the repository actually declares. An assembly-based check can only
/// see references that survived compilation, and cannot see a project that no test
/// happens to reference.
/// </remarks>
internal static partial class SolutionTree
{
    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<ProjectFile> Projects { get; } = LoadProjects();

    public static IReadOnlyList<SourceFile> Sources { get; } = LoadSources();

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SiraraCBA.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not locate the solution root (SiraraCBA.slnx).");
    }

    private static List<ProjectFile> LoadProjects() =>
        Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(ProjectFile.Load)
            .ToList();

    private static List<SourceFile> LoadSources() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => new SourceFile(
                Path.GetRelativePath(Root, path).Replace('\\', '/'),
                File.ReadAllLines(path)))
            .ToList();

    private static bool IsBuildOutput(string path)
    {
        string normalised = path.Replace('\\', '/');
        return normalised.Contains("/bin/", StringComparison.Ordinal)
               || normalised.Contains("/obj/", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\s*//")]
    public static partial Regex CommentLine();
}

/// <summary>One .csproj and the projects it references.</summary>
internal sealed record ProjectFile(string Name, string RelativePath, IReadOnlyList<string> ProjectReferences)
{
    public static ProjectFile Load(string path)
    {
        XDocument document = XDocument.Load(path);
        string directory = Path.GetDirectoryName(path)!;

        List<string> references = document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(
                Path.GetFullPath(Path.Combine(directory, value!.Replace('\\', '/')))))
            .ToList();

        return new ProjectFile(
            Path.GetFileNameWithoutExtension(path),
            Path.GetRelativePath(SolutionTree.Root, path).Replace('\\', '/'),
            references);
    }

    /// <summary>The module a project belongs to, or null for shared and host projects.</summary>
    public string? Module =>
        Name.StartsWith("SiraraCBA.Modules.", StringComparison.Ordinal)
            ? Name.Split('.')[2]
            : null;

    public string Layer => Name.Split('.')[^1];

    public bool IsTest => Name.EndsWith("Tests", StringComparison.Ordinal);
}

/// <summary>One source file, with its lines, for rules that must read code.</summary>
internal sealed record SourceFile(string RelativePath, IReadOnlyList<string> Lines)
{
    public bool IsIn(string pathFragment) =>
        RelativePath.Contains(pathFragment, StringComparison.OrdinalIgnoreCase);

    public IEnumerable<(int Number, string Text)> CodeLines =>
        Lines.Select((text, index) => (index + 1, text))
            .Where(line => !SolutionTree.CommentLine().IsMatch(line.Item2)
                           && !string.IsNullOrWhiteSpace(line.Item2));
}
