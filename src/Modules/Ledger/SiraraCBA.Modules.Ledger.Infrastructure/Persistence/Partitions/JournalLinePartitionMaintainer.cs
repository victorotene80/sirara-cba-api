using Microsoft.Extensions.Options;
using SiraraCBA.BuildingBlocks.Application.Abstractions;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence.Partitions;

/// <summary>
/// Creates the monthly partitions <c>journal_lines</c> needs before it needs them.
/// </summary>
/// <remarks>
/// Takes <see cref="IClock"/> rather than reading the machine clock: "which month is
/// next" must be testable without waiting for a month to pass (ER-7). Scheduling this
/// on a timer needs the module registered in a host, which is S04 — S03 builds and
/// proves the routine.
/// </remarks>
public sealed class JournalLinePartitionMaintainer
{
    private readonly LedgerDbContext _context;
    private readonly IClock _clock;
    private readonly PartitionOptions _options;

    public JournalLinePartitionMaintainer(
        LedgerDbContext context,
        IClock clock,
        IOptions<PartitionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _context = context;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// Ensures a partition exists for the current month and each of the next
    /// <see cref="PartitionOptions.MonthsAhead"/>. Safe to run repeatedly.
    /// </summary>
    public async Task<int> EnsurePartitionsAsync(CancellationToken cancellationToken)
    {
        DateOnly month = FirstOfMonth(_clock.BusinessToday);
        int created = 0;

        // The current month plus the configured look-ahead. Inclusive of both ends:
        // a table that runs out of partitions stops accepting postings, and the
        // failure mode is a bank that cannot take money.
        for (int ahead = 0; ahead <= _options.MonthsAhead; ahead++)
        {
            DateOnly from = month.AddMonths(ahead);
            DateOnly to = from.AddMonths(1);

            // `if not exists` is what makes this safe to run on a timer.
            string sql =
                $"""
                 create table if not exists {LedgerSchema.Name}.{PartitionName(from)}
                 partition of {LedgerSchema.Name}.{LedgerSchema.JournalLines}
                 for values from ('{from:yyyy-MM-dd}') to ('{to:yyyy-MM-dd}');
                 """;

            await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
            created++;
        }

        return created;
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static string PartitionName(DateOnly month) =>
        $"{LedgerSchema.JournalLines}_{month:yyyy_MM}";
}
