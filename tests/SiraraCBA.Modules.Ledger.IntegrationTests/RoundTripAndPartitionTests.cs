using Microsoft.Extensions.Options;
using SiraraCBA.BuildingBlocks.Application.Abstractions;
using SiraraCBA.Modules.Ledger.Domain.Accounts;
using SiraraCBA.Modules.Ledger.Domain.ChartOfAccounts;
using SiraraCBA.Modules.Ledger.Domain.Identifiers;
using SiraraCBA.Modules.Ledger.Domain.Journals;
using SiraraCBA.Modules.Ledger.Infrastructure.Persistence;
using SiraraCBA.Modules.Ledger.Infrastructure.Persistence.Partitions;
using SiraraCBA.SharedKernel.Monetary;
using SiraraCBA.SharedKernel.Primitives;

namespace SiraraCBA.Modules.Ledger.IntegrationTests;

/// <summary>C10 and C11: the routine that keeps partitions ahead, and a real round trip.</summary>
[Collection(LedgerDatabaseSuite.Name)]
public sealed class RoundTripAndPartitionTests
{
    private readonly LedgerDatabaseFixture _db;

    public RoundTripAndPartitionTests(LedgerDatabaseFixture db) => _db = db;

    [Fact]
    public async Task AnEntryBuiltByTheDomain_SurvivesARoundTrip_Intact()
    {
        // C11. The domain's guarantees are worth nothing if persistence loses the
        // currency, the scale, or the order of the lines.
        Currency ngn = _db.Currencies.Get("NGN").Value;
        Account debit = ActiveAccount(ngn, Guid.CreateVersion7().ToString()[^12..]);
        Account credit = ActiveAccount(ngn, Guid.CreateVersion7().ToString()[^12..]);
        ChartOfAccount leaf = PostableLeaf();

        Result<JournalEntry> built = JournalEntry.Create(
            new JournalId(Guid.CreateVersion7()),
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1),
            "round trip", "tests", $"rt-{Guid.CreateVersion7()}", Guid.CreateVersion7(),
            [
                new JournalLineDraft(new JournalLineId(Guid.CreateVersion7()), debit, leaf, DebitOrCredit.Debit, Money.Create(100.25m, ngn).Value),
                new JournalLineDraft(new JournalLineId(Guid.CreateVersion7()), credit, leaf, DebitOrCredit.Credit, Money.Create(100.25m, ngn).Value),
            ]);

        built.IsSuccess.ShouldBeTrue();

        await using (LedgerDbContext writing = _db.CreateOwnerContext())
        {
            writing.ChartOfAccounts.Add(leaf);
            writing.Accounts.AddRange(debit, credit);
            writing.JournalEntries.Add(built.Value);
            await writing.SaveChangesAsync();
        }

        await using LedgerDbContext reading = _db.CreateOwnerContext();

        JournalEntry read = await reading.JournalEntries
            .Include(entry => entry.Lines)
            .SingleAsync(entry => entry.Id == built.Value.Id);

        read.Currency.Code.ShouldBe("NGN");
        read.Lines.Count.ShouldBe(2);

        // Ordered explicitly. PostgreSQL returns rows in whatever order it likes, and
        // this first came back [2, 1] — nothing in the module guarantees statement
        // order yet, because no query layer exists until S04. Sorting here is honest
        // about what S03 proves (the data survives) and what it does not (that a
        // reader sees the lines in sequence). The latter is carried forward.
        List<JournalLine> ordered = [.. read.Lines.OrderBy(line => line.SequenceNumber)];

        ordered.Select(line => line.SequenceNumber).ShouldBe([1, 2]);
        ordered.Select(line => line.Amount.Amount).ShouldAllBe(amount => amount == 100.25m);
        ordered.Select(line => line.Amount.Currency.Code).ShouldAllBe(code => code == "NGN");

        // The scale survives too: 100.25 must not come back as 100.2 or 100.
        ordered[0].Amount.Amount.ShouldBe(100.25m);

        ordered[0].Direction.ShouldBe(DebitOrCredit.Debit);
        ordered[0].AccountId.ShouldBe(debit.Id);
        ordered[1].Direction.ShouldBe(DebitOrCredit.Credit);
        ordered[1].AccountId.ShouldBe(credit.Id);
    }

    [Fact]
    public async Task ThePartitionMaintainer_CreatesMonthsAhead_AndIsIdempotent()
    {
        // C10. A partitioned table that runs out of partitions stops accepting
        // postings — a bank that cannot take money. And the routine has to be safe to
        // run again, because it will be run on a timer.
        await using LedgerDbContext context = _db.CreateOwnerContext();

        JournalLinePartitionMaintainer maintainer = new(
            context,
            new FixedClock(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(new PartitionOptions { MonthsAhead = 3 }));

        await maintainer.EnsurePartitionsAsync(CancellationToken.None);

        long after = await CountPartitionsAsync();
        after.ShouldBeGreaterThanOrEqualTo(4, "the current month plus three ahead.");

        await maintainer.EnsurePartitionsAsync(CancellationToken.None);

        (await CountPartitionsAsync()).ShouldBe(after, "running it twice must create nothing new.");
    }

    private Task<long> CountPartitionsAsync() => _db.CountAsOwnerAsync(
        $"""
         select count(*) from pg_inherits i
         join pg_class parent on parent.oid = i.inhparent
         join pg_namespace n on n.oid = parent.relnamespace
         where n.nspname = '{LedgerSchema.Name}' and parent.relname = '{LedgerSchema.JournalLines}'
         """);

    /// <summary>This suite's own chart node — see the note in AppendOnlyTests.</summary>
    private const string OwnGlCode = "30101";

    private static ChartOfAccount PostableLeaf() =>
        ChartOfAccount.Create(new GlCode(OwnGlCode), "Customer deposits", AccountType.Liability,
            NormalBalance.Credit, new GlCode("301"), level: 2, isPostable: true).Value;

    private static Account ActiveAccount(Currency currency, string number) =>
        Account.Create(new AccountId(Guid.CreateVersion7()), new AccountNumber(number), null,
            "current", new GlCode(OwnGlCode), currency, AccountStatus.Active,
            new DateOnly(2026, 1, 1)).Value;

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }

        public DateOnly BusinessToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }
}
