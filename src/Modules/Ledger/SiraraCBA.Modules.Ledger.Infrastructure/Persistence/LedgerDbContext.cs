using Microsoft.EntityFrameworkCore.ChangeTracking;
using SiraraCBA.Modules.Ledger.Domain.Accounts;
using SiraraCBA.Modules.Ledger.Domain.ChartOfAccounts;
using SiraraCBA.Modules.Ledger.Domain.Journals;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

/// <summary>
/// The ledger module's only connection to its own schema. Nothing outside this
/// module opens one (I-7, D-8).
/// </summary>
public sealed class LedgerDbContext : DbContext
{
    private readonly ICurrencyRegistry _currencies;

    public LedgerDbContext(DbContextOptions<LedgerDbContext> options, ICurrencyRegistry currencies)
        : base(options) => _currencies = currencies;

    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    /// <summary>Resolves a stored ISO 4217 code back to a currency (ER-9).</summary>
    internal ICurrencyRegistry Currencies => _currencies;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DenormalisePostingDateOntoLines();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        DenormalisePostingDateOntoLines();
        return base.SaveChanges();
    }

    /// <summary>
    /// Copies each entry's posting date onto its lines.
    /// </summary>
    /// <remarks>
    /// <c>journal_lines.posting_date</c> exists only because PostgreSQL requires the
    /// partition key in the row and in every unique constraint — it is denormalised
    /// from the entry (ledger-spec.md §2). That is a storage concern, so it is a
    /// shadow property filled here rather than a field added to
    /// <c>JournalLine</c>: the domain does not change shape to suit the database.
    /// <para>
    /// Left unset it defaults to 0001-01-01, which no partition covers, and the
    /// insert fails with "no partition of relation journal_lines found for row".
    /// </para>
    /// </remarks>
    private void DenormalisePostingDateOntoLines()
    {
        foreach (EntityEntry<JournalEntry> entry in ChangeTracker.Entries<JournalEntry>())
        {
            foreach (JournalLine line in entry.Entity.Lines)
            {
                Entry(line).Property<DateOnly>("posting_date").CurrentValue = entry.Entity.PostingDate;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(LedgerSchema.Name);

        modelBuilder.ApplyConfiguration(new Configurations.ChartOfAccountConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AccountConfiguration(_currencies));
        modelBuilder.ApplyConfiguration(new Configurations.JournalEntryConfiguration(_currencies));
        modelBuilder.ApplyConfiguration(new Configurations.JournalLineConfiguration(_currencies));
    }
}
