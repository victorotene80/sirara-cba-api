using Npgsql;
using SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

namespace SiraraCBA.Modules.Ledger.IntegrationTests;

/// <summary>
/// C5–C9. The domain already refuses every one of these; this suite proves the
/// **database** refuses them too.
/// </summary>
/// <remarks>
/// Every attempt here is raw SQL that bypasses <c>JournalEntry.Create</c> entirely.
/// Going through the factory would prove nothing new — the point of I-3 and D-10 is
/// that immutability survives a migration, a maintenance script, or a future module
/// that has a connection string and no manners.
/// </remarks>
[Collection(LedgerDatabaseSuite.Name)]
public sealed class AppendOnlyTests
{
    private readonly LedgerDatabaseFixture _db;

    /// <summary>
    /// This suite's own chart node. The fixture is shared across test classes, so a
    /// fixed code collided with the round-trip suite's — isolation by uniqueness,
    /// never by which test happened to run first (coding-standards.md, Testing).
    /// </summary>
    private const string GlCode = "20101";

    public AppendOnlyTests(LedgerDatabaseFixture db) => _db = db;

    [Fact]
    public async Task TheDatabase_RefusesAnUpdate_ToAPostedEntry_AsTheApplicationRole()
    {
        string journalId = await SeedBalancedEntryAsync("upd-app");

        Func<Task> update = () => _db.ExecuteAsApplicationAsync(
            $"""
             update {LedgerSchema.Name}.{LedgerSchema.JournalEntries}
             set description = 'tampered' where journal_id = '{journalId}'
             """);

        await update.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesAnUpdate_ToAPostedEntry_EvenAsTheOwner()
    {
        // Grants do not bind an owner. The raising trigger is what makes this true
        // for everyone, which is why I-3 asks for both.
        string journalId = await SeedBalancedEntryAsync("upd-own");

        Func<Task> update = () => _db.ExecuteAsOwnerAsync(
            $"""
             update {LedgerSchema.Name}.{LedgerSchema.JournalEntries}
             set description = 'tampered' where journal_id = '{journalId}'
             """);

        await update.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesADelete_OfAPostedEntry_AsTheApplicationRole()
    {
        string journalId = await SeedBalancedEntryAsync("del-app");

        Func<Task> delete = () => _db.ExecuteAsApplicationAsync(
            $"delete from {LedgerSchema.Name}.{LedgerSchema.JournalEntries} where journal_id = '{journalId}'");

        await delete.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesADelete_OfAPostedEntry_EvenAsTheOwner()
    {
        string journalId = await SeedBalancedEntryAsync("del-own");

        Func<Task> delete = () => _db.ExecuteAsOwnerAsync(
            $"delete from {LedgerSchema.Name}.{LedgerSchema.JournalEntries} where journal_id = '{journalId}'");

        await delete.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesAnUnbalancedEntry_AtCommit()
    {
        // I-1 at rest. The check is deferred, so it fires at COMMIT rather than on
        // the first line — an entry is only balanced once all its lines are in.
        Func<Task> unbalanced = () => SeedEntryAsync("unbal", debit: 100m, credit: 99m);

        await unbalanced.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesALine_WhoseCurrencyDiffersFromItsEntry()
    {
        // I-2 at rest.
        Func<Task> mixed = () => SeedEntryAsync("mixed", debit: 100m, credit: 100m, creditCurrency: "JPY");

        await mixed.ShouldThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task TheDatabase_RefusesASingleLineEntry()
    {
        // Proves the behaviour, not a particular guard. Mutation testing showed the
        // trigger's explicit `lines < 2` branch is unreachable from here: a single
        // line is necessarily unbalanced, so the balance check refuses it first and
        // disabling `lines < 2` changes nothing. The branch is defence in depth
        // against a future state — a zero-amount line, say — that other constraints
        // happen to block today. It is annotated in the SQL so nobody deletes it
        // believing it is covered, and nobody believes this test covers it.
        string journalId = Guid.CreateVersion7().ToString();
        string accountId = await EnsureAccountAsync("NGN");

        Func<Task> singleLine = () => _db.ExecuteAsOwnerAsync(
            $"""
             begin;
             insert into {LedgerSchema.Name}.{LedgerSchema.JournalEntries}
               (journal_id, transaction_date, value_date, posting_date, description,
                source_system, source_reference, currency, status, created_by, created_at)
             values ('{journalId}', date '{Seed:yyyy-MM-dd}', date '{Seed:yyyy-MM-dd}',
                     date '{Seed:yyyy-MM-dd}', 'one line', 'tests', 'single-{journalId}',
                     'NGN', 'posted', '{Guid.CreateVersion7()}', now());

             insert into {LedgerSchema.Name}.{LedgerSchema.JournalLines}
               (line_id, journal_id, account_id, debit_or_credit, amount, currency,
                sequence_number, posting_date)
             values ('{Guid.CreateVersion7()}', '{journalId}', '{accountId}', 'debit',
                     100, 'NGN', 1, date '{Seed:yyyy-MM-dd}');
             commit;
             """);

        await singleLine.ShouldThrowAsync<PostgresException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task TheDatabase_RefusesANonPositiveAmount(decimal amount)
    {
        // I-8 at rest. A negative debit is a credit; the database must not accept
        // the second spelling either.
        Func<Task> nonPositive = () => SeedEntryAsync($"amt{amount}", debit: amount, credit: amount);

        await nonPositive.ShouldThrowAsync<PostgresException>();
    }

    private static DateOnly Seed => LedgerDatabaseFixture.TestPostingDate;

    private Task<string> SeedBalancedEntryAsync(string reference) =>
        SeedEntryAsync(reference, debit: 100m, credit: 100m);

    private async Task<string> SeedEntryAsync(
        string reference,
        decimal debit,
        decimal credit,
        string currency = "NGN",
        string? creditCurrency = null)
    {
        string journalId = Guid.CreateVersion7().ToString();
        string accountId = await EnsureAccountAsync(currency);

        await _db.ExecuteAsOwnerAsync(
            $"""
             begin;
             insert into {LedgerSchema.Name}.{LedgerSchema.JournalEntries}
               (journal_id, transaction_date, value_date, posting_date, description,
                source_system, source_reference, currency, status, created_by, created_at)
             values ('{journalId}', date '2026-03-01', date '2026-03-01', date '2026-03-01',
                     'seed', 'tests', '{reference}', '{currency}', 'posted',
                     '{Guid.CreateVersion7()}', now());

             insert into {LedgerSchema.Name}.{LedgerSchema.JournalLines}
               (line_id, journal_id, account_id, debit_or_credit, amount, currency,
                sequence_number, posting_date)
             values ('{Guid.CreateVersion7()}', '{journalId}', '{accountId}', 'debit',
                     {debit.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                     '{currency}', 1, date '2026-03-01'),
                    ('{Guid.CreateVersion7()}', '{journalId}', '{accountId}', 'credit',
                     {credit.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                     '{creditCurrency ?? currency}', 2, date '2026-03-01');
             commit;
             """);

        return journalId;
    }

    private async Task<string> EnsureAccountAsync(string currency)
    {
        string accountId = Guid.CreateVersion7().ToString();

        // Not a prefix of the id: UUIDv7 is time-ordered, so two ids generated
        // milliseconds apart share their leading characters and the unique index on
        // account_number rejected the second one. The suffix is the random half.
        string accountNumber = accountId[^12..];

        await _db.ExecuteAsOwnerAsync(
            $"""
             insert into {LedgerSchema.Name}.{LedgerSchema.ChartOfAccounts}
               (gl_code, gl_name, account_type, normal_balance, parent_gl_code, level, is_postable)
             values ('{GlCode}', 'Customer deposits', 'liability', 'credit', null, 0, false)
             on conflict (gl_code) do nothing;

             insert into {LedgerSchema.Name}.{LedgerSchema.Accounts}
               (account_id, account_number, customer_id, product_type, gl_code, currency, status, opened_at)
             values ('{accountId}', '{accountNumber}', null, 'current', '{GlCode}',
                     '{currency}', 'active', now());
             """);

        return accountId;
    }
}
