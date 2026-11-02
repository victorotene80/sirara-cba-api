using Microsoft.EntityFrameworkCore.Design;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> target this project directly.
/// </summary>
/// <remarks>
/// This is what keeps S03 out of the Api host. Wiring the module into the host would
/// engage <c>AModuleWiredIntoTheHostHasAllFourLayers</c> and ER-6 immediately, and
/// both would fail — the module has no Application layer and no <c>IModule</c> until
/// S04. Migrations do not need a host; they need a context.
/// <para>
/// The connection string here is for design-time tooling only. Nothing runs against
/// it, and it carries no credential.
/// </para>
/// </remarks>
public sealed class LedgerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql("Host=localhost;Database=sirara_design_time")
                .Options,
            new InMemoryCurrencyRegistry(DesignTimeCurrencies));

    /// <summary>
    /// Only used to build the model at design time. Real currencies come from
    /// configuration through <c>ICurrencyRegistry</c> (ER-9).
    /// </summary>
    private static IReadOnlyCollection<Currency> DesignTimeCurrencies { get; } =
        [Currency.Create("NGN", 2).Value];
}
