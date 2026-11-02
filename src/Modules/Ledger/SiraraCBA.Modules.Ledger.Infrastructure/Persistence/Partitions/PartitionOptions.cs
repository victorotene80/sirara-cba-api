using System.ComponentModel.DataAnnotations;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence.Partitions;

/// <summary>
/// How <c>journal_lines</c> is partitioned and how far ahead partitions are created.
/// </summary>
/// <remarks>
/// Configuration, not literals (ER-9, O-5). Granularity is monthly by default and
/// the look-ahead is a window, because a partitioned table that runs out of
/// partitions stops accepting postings — the failure mode is a bank that cannot
/// take money, so the margin is a setting an operator can widen.
/// </remarks>
public sealed class PartitionOptions
{
    public const string SectionName = "Ledger:Partitions";

    /// <summary>Months of partitions to keep ahead of the current business month.</summary>
    [Range(1, 60)]
    public int MonthsAhead { get; init; } = 3;
}
