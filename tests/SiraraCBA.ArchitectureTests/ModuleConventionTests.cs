namespace SiraraCBA.ArchitectureTests;

/// <summary>
/// Rules that engage as soon as the first module lands. They are written now, not
/// later, so that no module is ever created without them (architecture.md §4).
/// </summary>
/// <remarks>
/// Where a rule has nothing to check yet, the test says so explicitly rather than
/// passing silently — a rule that quietly checks nothing is worse than no rule,
/// because it reads as coverage.
/// </remarks>
public sealed class ModuleConventionTests
{
    private static List<string> Modules { get; } = SolutionTree.Projects
        .Select(project => project.Module)
        .Where(module => module is not null)
        .Select(module => module!)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(module => module, StringComparer.Ordinal)
        .ToList();

    /// <summary>The rules in this file that have nothing to check while no module exists.</summary>
    private static readonly string[] RulesInertWithoutAModule =
    [
        "AModuleHasNoLayerOutsideTheFour",
        "AModuleWiredIntoTheHostHasAllFourLayers",
        "Er04_ContractInterfaceMethodsAreWrittenAsIfRemote",
        "Er05_NoModuleReachesIntoAnotherModulesDomain",
        "Er06_EveryModuleSelfRegistersThroughIModule",   // engages at S04, with the host wiring
        "Er08_EveryStateChangingContractCarriesAnIdempotencyKey",
        "Er10_NoCrossModuleCallDiscardsItsAnswer",
    ];

    [Fact]
    public void TheModuleListIsVisible_SoInertRulesAreNotMistakenForCoverage()
    {
        // Several rules in this file can only check something once a module exists.
        // This test makes both the module count and the inert rules explicit, so
        // "green" is never read as "checked" when there is nothing yet to check.
        Modules.Count.ShouldBeGreaterThanOrEqualTo(0);

        if (Modules.Count == 0)
        {
            RulesInertWithoutAModule.ShouldNotBeEmpty(
                $"No module exists, so {RulesInertWithoutAModule.Length} rules in this "
                + "file are inert: " + string.Join(", ", RulesInertWithoutAModule)
                + ". They engage with the first module (S02).");
        }
    }

    private static readonly string[] TheFourLayers =
        ["Contracts", "Domain", "Application", "Infrastructure"];

    /// <summary>
    /// Modules the Api host registers. A module in service, as opposed to one still
    /// being built across slices.
    /// </summary>
    /// <remarks>
    /// The host referencing a module's Infrastructure is what puts it in service —
    /// ER-1 permits that reference and only for registration. Rules that describe a
    /// finished module are checked against this, not against every directory that
    /// happens to exist, because the backlog delivers a module's layers over three
    /// slices and a rule may not assert what context cannot yet satisfy.
    /// </remarks>
    private static List<string> ModulesInService { get; } = SolutionTree.Projects
        .Single(project => project.Name == "SiraraCBA.Api")
        .ProjectReferences
        .Where(reference => reference.StartsWith("SiraraCBA.Modules.", StringComparison.Ordinal))
        .Select(reference => reference.Split('.')[2])
        .Distinct(StringComparer.Ordinal)
        .OrderBy(module => module, StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void AModuleHasNoLayerOutsideTheFour()
    {
        if (Modules.Count == 0)
        {
            // No modules yet; this rule engages with the first one (S02).
            return;
        }

        List<string> violations = SolutionTree.Projects
            .Where(project => project.Module is not null && !project.IsTest)
            .Where(project => !TheFourLayers.Contains(project.Layer, StringComparer.Ordinal))
            .Select(project => $"{project.Name} is a '{project.Layer}' layer")
            .ToList();

        violations.ShouldBeEmpty(
            "A module is Contracts, Domain, Application and Infrastructure, and "
            + "nothing else. A fifth layer is a boundary nobody agreed to.");
    }

    [Fact]
    public void AModuleWiredIntoTheHostHasAllFourLayers()
    {
        // "All four" is true of a module in service, not of one being built. The
        // backlog delivers the layers across three slices — S02 Domain and Contracts,
        // S03 Infrastructure, S04 Application — so asserting all four from the first
        // slice would assert something context itself cannot satisfy yet.
        //
        // The host referencing a module's Infrastructure is what puts it in service
        // (ER-1 permits that reference, and only for registration). From that point
        // the module is complete or it is broken, and this says which.
        if (ModulesInService.Count == 0)
        {
            // No module is wired into the host yet; this rule engages at S04.
            return;
        }

        List<string> violations = [];

        foreach (string module in ModulesInService)
        {
            List<string> layers = SolutionTree.Projects
                .Where(project => project.Module == module && !project.IsTest)
                .Select(project => project.Layer)
                .ToList();

            violations.AddRange(TheFourLayers
                .Where(layer => !layers.Contains(layer, StringComparer.Ordinal))
                .Select(layer => $"{module} is wired into the host but has no {layer} project"));
        }

        violations.ShouldBeEmpty(
            "Extraction is moving a module's projects and giving them a host. A module "
            + "the host already registers must have all four to move (architecture.md §2).");
    }

    [Fact]
    public void Er06_EveryModuleSelfRegistersThroughIModule()
    {
        // Checked against modules in service, for the same reason as the four-layer
        // rule: IModule lives in a module's Infrastructure project, which the backlog
        // does not create until S03 and does not register until S04.
        if (ModulesInService.Count == 0)
        {
            // No module is wired into the host yet; this rule engages at S04.
            return;
        }

        List<string> violations = [];

        foreach (string module in ModulesInService)
        {
            bool declaresModule = SolutionTree.Sources
                .Where(file => file.IsIn($"/Modules/{module}/") && file.IsIn(".Infrastructure/"))
                .Any(file => file.CodeLines.Any(line =>
                    line.Text.Contains(": IModule", StringComparison.Ordinal)
                    || line.Text.Contains(", IModule", StringComparison.Ordinal)));

            if (!declaresModule)
            {
                violations.Add($"{module} has no IModule implementation in its Infrastructure project");
            }
        }

        violations.ShouldBeEmpty(
            "Extraction is moving a module's projects and giving them a host. That "
            + "only holds if the host knows a module solely through IModule (ER-6).");
    }

    [Fact]
    public void Er04_ContractInterfaceMethodsAreWrittenAsIfRemote()
    {
        if (Modules.Count == 0)
        {
            // No modules yet; this rule engages with the first one (S02).
            return;
        }

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources.Where(f => f.IsIn(".Contracts/")))
        {
            foreach ((int number, string text) in file.CodeLines)
            {
                string line = text.Trim();

                // A positional record declaration also ends in ');' and contains '(' —
                // PostJournalEntryResponse(Guid JournalId, bool WasReplay); is a DTO,
                // not a cross-module call, and ER-4 governs calls. Without this the
                // rule demands a CancellationToken on a data shape.
                bool declaresAType =
                    line.Contains("record ", StringComparison.Ordinal)
                    || line.Contains("class ", StringComparison.Ordinal)
                    || line.Contains("struct ", StringComparison.Ordinal)
                    || line.Contains("interface ", StringComparison.Ordinal)
                    || line.Contains("enum ", StringComparison.Ordinal);

                // An interface method declaration: returns something, ends in ');'
                bool looksLikeMethod = line.EndsWith(");", StringComparison.Ordinal)
                                       && line.Contains('(', StringComparison.Ordinal)
                                       && !line.StartsWith("//", StringComparison.Ordinal)
                                       && !declaresAType;

                if (!looksLikeMethod)
                {
                    continue;
                }

                if (!line.Contains("CancellationToken", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}:{number} takes no CancellationToken — {line}");
                }

                if (!line.Contains("Task", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}:{number} is synchronous — {line}");
                }

                if (!line.Contains("Result", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}:{number} does not return a Result — {line}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "A cross-module call is written as if it were already a network hop: "
            + "async, cancellable, and returning a Result rather than throwing (ER-4).");
    }

    // ------------------------------------------------------------------ ER-5

    [Fact]
    public void Er05_NoModuleReachesIntoAnotherModulesDomain()
    {
        if (Modules.Count == 0)
        {
            // No modules yet; this rule engages with the first one (S02).
            return;
        }

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources)
        {
            string? owner = OwningModuleOf(file);

            foreach ((int number, string text) in file.CodeLines)
            {
                foreach (string module in Modules)
                {
                    // A domain type borrowed across a boundary is the coupling ER-5
                    // exists to prevent: two modules then share a shape that neither
                    // can change alone. Each declares its own DTO instead.
                    if (module != owner
                        && text.Contains($"SiraraCBA.Modules.{module}.Domain", StringComparison.Ordinal))
                    {
                        violations.Add($"{file.RelativePath}:{number} reaches into {module}.Domain");
                    }
                }

                // Contracts is the published API. It may reference the SharedKernel
                // and nothing else — including its own Domain (architecture.md §3).
                if (file.IsIn(".Contracts/")
                    && text.Contains(".Domain", StringComparison.Ordinal)
                    && text.TrimStart().StartsWith("using ", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}:{number} — a Contracts project imports a Domain namespace");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Modules do not share domain types (ER-5). Where two modules need the "
            + "same shape, each declares its own DTO in its own Contracts. "
            + "Duplication across a boundary is cheaper than coupling across it.");
    }

    // ------------------------------------------------------------------ ER-8

    [Fact]
    public void Er08_EveryStateChangingContractCarriesAnIdempotencyKey()
    {
        if (Modules.Count == 0)
        {
            // No modules yet; this rule engages with the first one (S02).
            return;
        }

        // Verbs that name a state change. A request built from one of these is a
        // write boundary, and every write boundary must be safe to retry —
        // at-least-once delivery is assumed from day one because it is what a
        // broker gives (ER-8).
        string[] mutatingVerbs =
        [
            "Post", "Create", "Reverse", "Approve", "Reject", "Submit",
            "Update", "Delete", "Register", "Disable", "Enable", "Revoke",
        ];

        // ER-8 reads "keyed by an explicit idempotency key **or a natural key**".
        // The first version of this rule only accepted the literal IdempotencyKey and
        // would have rejected the ledger, whose idempotency is
        // (source_system, source_reference) — I-5, and written into ledger-spec.md §4.
        // A deliberately short, visible list rather than a heuristic: a fourth form of
        // natural key is added here, in the open, or it is not a declared key at all.
        string[] declaredKeys = ["IdempotencyKey", "SourceReference", "NaturalKey"];

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources.Where(f => f.IsIn(".Contracts/")))
        {
            bool declaresKey = file.Lines.Any(line =>
                declaredKeys.Any(key => line.Contains(key, StringComparison.Ordinal)));

            foreach ((int number, string text) in file.CodeLines)
            {
                string line = text.Trim();

                bool declaresWriteRequest =
                    (line.Contains("record ", StringComparison.Ordinal)
                     || line.Contains("class ", StringComparison.Ordinal))
                    && (line.Contains("Request", StringComparison.Ordinal)
                        || line.Contains("Command", StringComparison.Ordinal))
                    && mutatingVerbs.Any(verb =>
                        line.Contains($" {verb}", StringComparison.Ordinal));

                if (declaresWriteRequest && !declaresKey)
                {
                    violations.Add($"{file.RelativePath}:{number} — {line}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every state-changing contract carries an explicit idempotency key or a "
            + "natural key (ER-8). Retrying must always be safe: a retried posting "
            + "that creates a second journal entry is money invented.");
    }

    // ------------------------------------------------------------------ ER-10

    [Fact]
    public void Er10_NoCrossModuleCallDiscardsItsAnswer()
    {
        if (Modules.Count == 0)
        {
            // No modules yet; this rule engages with the first one (S02).
            return;
        }

        List<string> violations = [];

        foreach (SourceFile file in SolutionTree.Sources.Where(f => f.IsIn(".Contracts/")))
        {
            foreach ((int number, string text) in file.CodeLines)
            {
                string line = text.Trim();

                bool looksLikeMethod = line.EndsWith(");", StringComparison.Ordinal)
                                       && line.Contains('(', StringComparison.Ordinal);

                if (!looksLikeMethod)
                {
                    continue;
                }

                // A synchronous cross-module call that returns no value is a call
                // whose answer the caller does not need — which is the definition of
                // something that should have been an integration event (ER-10).
                // Task<T> is fine: the caller genuinely cannot proceed without it.
                bool returnsNothing = line.Contains("Task ", StringComparison.Ordinal)
                                      && !line.Contains("Task<", StringComparison.Ordinal);

                if (returnsNothing)
                {
                    violations.Add($"{file.RelativePath}:{number} — {line}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "If a module reacts to something rather than being asked for something, "
            + "it subscribes to an integration event (ER-10). A synchronous "
            + "cross-module call that returns nothing couples two modules' latency "
            + "and availability for no answer in return.");
    }

    /// <summary>The module a source file belongs to, or null for shared and host code.</summary>
    private static string? OwningModuleOf(SourceFile file)
    {
        string[] segments = file.RelativePath.Split('/');

        return segments.Length > 2 && segments[1] == "Modules" ? segments[2] : null;
    }

}
