using System.Reflection;
using SiraraCBA.BuildingBlocks.Infrastructure;
using SiraraCBA.BuildingBlocks.Infrastructure.Modules;
using SiraraCBA.BuildingBlocks.Infrastructure.Observability;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddBuildingBlocks(builder.Configuration);
builder.Services.AddTelemetry(builder.Configuration);

// Modules self-register. The host lists the assemblies to scan and does nothing
// else — no business logic, no routes of its own (architecture.md §7).
IReadOnlyList<IModule> modules = builder.Services.AddModules(
    builder.Configuration,
    ModuleAssemblies.All);

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");
app.MapModules(modules);

await app.RunAsync().ConfigureAwait(false);

/// <summary>The assemblies scanned for modules. One line per module, and nothing else.</summary>
internal static class ModuleAssemblies
{
    // Empty until the first module lands (S02–S04 Ledger, S07 Identity,
    // S09–S11 Approvals). Adding one is adding a project reference and one entry.
    public static readonly Assembly[] All = [];
}

/// <summary>Exposed so integration tests can host the API in-process.</summary>
public partial class Program;
