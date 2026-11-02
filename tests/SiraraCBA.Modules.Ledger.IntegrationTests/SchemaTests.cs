using SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

namespace SiraraCBA.Modules.Ledger.IntegrationTests;

/// <summary>
/// C1–C3: the migration produced the schema `ledger-spec.md` §2 describes, from an
/// empty database, unattended.
/// </summary>
[Collection(LedgerDatabaseSuite.Name)]
public sealed class SchemaTests
{
    private readonly LedgerDatabaseFixture _db;

    public SchemaTests(LedgerDatabaseFixture db) => _db = db;

    [Theory]
    [InlineData(LedgerSchema.ChartOfAccounts)]
    [InlineData(LedgerSchema.Accounts)]
    [InlineData(LedgerSchema.JournalEntries)]
    [InlineData(LedgerSchema.JournalLines)]
    [InlineData(LedgerSchema.Balances)]
    public async Task Migrations_Create_EveryTable_InTheLedgerSchema(string table)
    {
        long found = await _db.CountAsOwnerAsync(
            $"""
             select count(*) from information_schema.tables
             where table_schema = '{LedgerSchema.Name}' and table_name = '{table}'
             """);

        found.ShouldBe(1);
    }

    [Fact]
    public async Task MigrationsHistory_LivesInTheLedgerSchema_AndNowhereElse()
    {
        // ER-2. EF's default is public.__EFMigrationsHistory: one table outside every
        // module's schema, which Identity and Approvals would share the moment they
        // migrate. Found by the step-38 review, not by Er02_, which only looks for
        // another module's schema name.
        long inLedger = await _db.CountAsOwnerAsync(
            $"""
             select count(*) from information_schema.tables
             where table_schema = '{LedgerSchema.Name}' and table_name = '{LedgerSchema.MigrationsHistory}'
             """);

        long elsewhere = await _db.CountAsOwnerAsync(
            $"""
             select count(*) from information_schema.tables
             where table_schema <> '{LedgerSchema.Name}' and table_name = '{LedgerSchema.MigrationsHistory}'
             """);

        inLedger.ShouldBe(1);
        elsewhere.ShouldBe(0, "No module's migrations history may live outside its own schema.");
    }

    [Fact]
    public async Task JournalLines_IsRangePartitioned_ByPostingDate()
    {
        // Not "a partition exists" — that the table itself is declared partitioned,
        // and by the column the spec names. O-5: monthly RANGE on posting_date.
        string? strategy = await _db.ScalarAsOwnerAsync<string>(
            $"""
             select p.partstrat::text
             from pg_partitioned_table p
             join pg_class c on c.oid = p.partrelid
             join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = '{LedgerSchema.Name}' and c.relname = '{LedgerSchema.JournalLines}'
             """);

        strategy.ShouldBe("r", "journal_lines must be RANGE partitioned (O-5).");

        string? column = await _db.ScalarAsOwnerAsync<string>(
            $"""
             select a.attname
             from pg_partitioned_table p
             join pg_class c on c.oid = p.partrelid
             join pg_namespace n on n.oid = c.relnamespace
             join pg_attribute a on a.attrelid = c.oid and a.attnum = p.partattrs[0]   -- int2vector is 0-based
             where n.nspname = '{LedgerSchema.Name}' and c.relname = '{LedgerSchema.JournalLines}'
             """);

        column.ShouldBe("posting_date");
    }

    [Fact]
    public async Task SourceSystemAndSourceReference_AreUniqueTogether()
    {
        // I-5. Idempotency is a database guarantee, not a race the application hopes
        // to win.
        long indexes = await _db.CountAsOwnerAsync(
            $"""
             select count(*) from pg_indexes
             where schemaname = '{LedgerSchema.Name}'
               and tablename = '{LedgerSchema.JournalEntries}'
               and indexdef ilike '%unique%'
               and indexdef ilike '%source_system%'
               and indexdef ilike '%source_reference%'
             """);

        indexes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CustomerId_IsAnOpaqueColumn_WithNoForeignKey()
    {
        // O-4, ER-2. This is the slice that turns the decision into a migration.
        long foreignKeys = await _db.CountAsOwnerAsync(
            $"""
             select count(*)
             from information_schema.table_constraints tc
             join information_schema.key_column_usage k on k.constraint_name = tc.constraint_name
             where tc.table_schema = '{LedgerSchema.Name}'
               and tc.table_name = '{LedgerSchema.Accounts}'
               and tc.constraint_type = 'FOREIGN KEY'
               and k.column_name = 'customer_id'
             """);

        foreignKeys.ShouldBe(0, "customer_id is an opaque value, never a foreign key (O-4, ER-2).");
    }
}
