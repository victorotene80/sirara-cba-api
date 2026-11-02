namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

/// <summary>
/// The one place the ledger's PostgreSQL options are set.
/// </summary>
/// <remarks>
/// EF keeps its migrations history in <c>public</c> unless told otherwise — a table
/// outside every module's schema that each module's migrations would share (ER-2).
/// Every caller that builds a <see cref="LedgerDbContext"/> goes through here, so the
/// history cannot land in <c>public</c> because one of them forgot.
/// </remarks>
public static class LedgerNpgsql
{
    public static DbContextOptionsBuilder<LedgerDbContext> UseLedgerNpgsql(
        this DbContextOptionsBuilder<LedgerDbContext> builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(LedgerSchema.MigrationsHistory, LedgerSchema.Name));
    }
}
