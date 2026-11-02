namespace SiraraCBA.ArchitectureTests;

/// <summary>
/// architecture.md §4, ER-1…ER-10. These are the rules that make "split into
/// microservices later" true rather than aspirational, so they are enforced from
/// the first slice — a boundary rule added after the code exists is a migration;
/// added before, it is free.
/// </summary>
public sealed class ExtractionReadinessTests
{
    // ------------------------------------------------------------------ ER-1

    [Fact]
    public void Er01_NoProjectReferencesAnotherModulesInternals()
    {
        List<string> violations = [];

        foreach (ProjectFile project in SolutionTree.Projects)
        {
            foreach (string reference in project.ProjectReferences)
            {
                if (!reference.StartsWith("SiraraCBA.Modules.", StringComparison.Ordinal))
                {
                    continue;
                }

                string referencedModule = reference.Split('.')[2];
                string referencedLayer = reference.Split('.')[^1];

                bool isOwnModule = project.Module == referencedModule;
                bool isContracts = referencedLayer == "Contracts";
                bool isHostRegisteringInfrastructure =
                    project.Name == "SiraraCBA.Api" && referencedLayer == "Infrastructure";

                if (isOwnModule || isContracts || isHostRegisteringInfrastructure || project.IsTest)
                {
                    continue;
                }

                violations.Add($"{project.Name} -> {reference}");
            }
        }

        violations.ShouldBeEmpty(
            "A module is reachable only through its Contracts project. "
            + "The Api host may reference a module's Infrastructure, for registration only.");
    }

    [Fact]
    public void Er01_ContractsProjectsDependOnNothingButTheSharedKernel()
    {
        List<string> violations = SolutionTree.Projects
            .Where(project => project.Layer == "Contracts")
            .SelectMany(project => project.ProjectReferences
                .Where(reference => reference != "SiraraCBA.SharedKernel")
                .Select(reference => $"{project.Name} -> {reference}"))
            .ToList();

        violations.ShouldBeEmpty(
            "A Contracts project is a module's published API. If it drags in the "
            + "module's Domain, every consumer inherits the module's internals.");
    }

    [Fact]
    public void Er01_DomainProjectsDependOnNothingButTheSharedKernel()
    {
        List<string> violations = SolutionTree.Projects
            .Where(project => project.Layer == "Domain")
            .SelectMany(project => project.ProjectReferences
                .Where(reference => reference != "SiraraCBA.SharedKernel")
                .Select(reference => $"{project.Name} -> {reference}"))
            .ToList();

        violations.ShouldBeEmpty("Domain has no outward reference of any kind (architecture.md §3).");
    }

    [Fact]
    public void Er01_ApplicationProjectsDoNotReferenceInfrastructure()
    {
        List<string> violations = SolutionTree.Projects
            .Where(project => project.Layer == "Application" && !project.IsTest)
            .SelectMany(project => project.ProjectReferences
                .Where(reference => reference.EndsWith(".Infrastructure", StringComparison.Ordinal))
                .Select(reference => $"{project.Name} -> {reference}"))
            .ToList();

        violations.ShouldBeEmpty("Dependencies point inward. Application declares interfaces; Infrastructure implements them.");
    }

    [Fact]
    public void Er01_TheSharedKernelDependsOnNothing()
    {
        ProjectFile sharedKernel = SolutionTree.Projects.Single(p => p.Name == "SiraraCBA.SharedKernel");

        sharedKernel.ProjectReferences.ShouldBeEmpty();
    }

    [Fact]
    public void Er01_TheApiHostDoesNotReferenceAnyModulesDomainOrApplication()
    {
        ProjectFile api = SolutionTree.Projects.Single(p => p.Name == "SiraraCBA.Api");

        api.ProjectReferences
            .Where(reference => reference.EndsWith(".Domain", StringComparison.Ordinal)
                                || (reference.EndsWith(".Application", StringComparison.Ordinal)
                                    && reference.StartsWith("SiraraCBA.Modules.", StringComparison.Ordinal)))
            .ShouldBeEmpty("The composition root wires modules. It does not reach into them (architecture.md §7).");
    }

    // ------------------------------------------------------------------ ER-2

    [Fact]
    public void Er02_NoSqlOrMigrationReferencesAnotherModulesSchema()
    {
        string[] schemas = ["ledger", "identity", "approvals"];
        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources.Where(f => f.IsIn("/Modules/")))
        {
            string owningModule = file.RelativePath.Split('/')[2].ToLowerInvariant();

            foreach ((int number, string text) in file.CodeLines)
            {
                foreach (string schema in schemas.Where(s => s != owningModule))
                {
                    if (text.Contains($"\"{schema}\".", StringComparison.OrdinalIgnoreCase)
                        || text.Contains($"{schema}.\"", StringComparison.OrdinalIgnoreCase)
                        || text.Contains($"schema: \"{schema}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{file.RelativePath}:{number} references schema '{schema}'");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "One schema per module, and no object crosses a schema boundary. "
            + "A cross-schema foreign key is a join that cannot survive extraction.");
    }

    // ------------------------------------------------------------------ ER-3

    [Fact]
    public void Er03_NothingOpensATransactionSpanningModules()
    {
        List<string> violations = SolutionTree.Sources
            .SelectMany(file => file.CodeLines
                .Where(line => line.Text.Contains("TransactionScope", StringComparison.Ordinal))
                .Select(line => $"{file.RelativePath}:{line.Number}"))
            .ToList();

        violations.ShouldBeEmpty(
            "Each module commits its own unit of work. A shared transaction is the "
            + "one change that makes later extraction impossible (ER-3); cross-module "
            + "consistency goes through the outbox.");
    }

    // ------------------------------------------------------------------ ER-7

    [Fact]
    public void Er07_TimeAndIdentifiersComeFromAbstractionsOutsideInfrastructure()
    {
        string[] forbidden =
        [
            "DateTime.UtcNow",
            "DateTime.Now",
            "DateTimeOffset.UtcNow",
            "DateTimeOffset.Now",
            "DateTime.Today",
            "Guid.NewGuid()",
            "Guid.CreateVersion7()",
            "Environment.TickCount",
            "new Random(",
        ];

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources)
        {
            // BuildingBlocks.Infrastructure is where these primitives are allowed to
            // exist — exactly once, behind IClock and IIdGenerator.
            if (file.IsIn("BuildingBlocks.Infrastructure/"))
            {
                continue;
            }

            foreach ((int number, string text) in file.CodeLines)
            {
                foreach (string token in forbidden.Where(t => text.Contains(t, StringComparison.Ordinal)))
                {
                    violations.Add($"{file.RelativePath}:{number} uses {token}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Time comes from IClock and identifiers from IIdGenerator (ER-7). "
            + "Otherwise an expiry, a value date or a rate lock cannot be tested "
            + "without waiting for real time to pass.");
    }

    // ------------------------------------------------------------------ ER-9

    [Fact]
    public void Er09_NoBusinessLiteralsInDomainOrApplication()
    {
        // Deliberately narrow: comparisons and durations built from a bare number
        // are where a threshold, a retry count or a lifetime hides. 0 and 1 are
        // excluded because they are structural, not policy.
        System.Text.RegularExpressions.Regex[] patterns =
        [
            new(@"TimeSpan\.From\w+\(\s*\d+"),
            new(@"[<>]=?\s*\d{2,}"),
            new(@"==\s*\d{2,}"),
        ];

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources
                     .Where(f => f.IsIn("/Domain/") || f.IsIn(".Domain/")
                                 || f.IsIn("/Application/") || f.IsIn(".Application/")))
        {
            foreach ((int number, string text) in file.CodeLines)
            {
                if (patterns.Any(pattern => pattern.IsMatch(text)))
                {
                    violations.Add($"{file.RelativePath}:{number} — {text.Trim()}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Limits, thresholds and lifetimes are configuration or data, never "
            + "literals (ER-9). If a number has business meaning, name it.");
    }
}
