namespace SiraraCBA.Modules.Ledger.Infrastructure.Migrations;

/// <summary>
/// The parts of the ledger's storage EF Core cannot express, kept as readable SQL
/// rather than buried in a migration's string literals.
/// </summary>
/// <remarks>
/// Everything here exists because <c>ledger-spec.md</c> §1 asks for the invariants to
/// hold **at rest**, not only in the domain. The domain stops code that goes through
/// it; these stop a migration, a maintenance script, or a future module that has a
/// connection string and no manners.
/// </remarks>
internal static class LedgerHardening
{
    /// <summary>Turns <c>journal_lines</c> into a RANGE-partitioned table (O-5).</summary>
    /// <remarks>
    /// EF creates an ordinary table. PostgreSQL cannot convert one in place, so the
    /// table is recreated as partitioned before anything is in it. The primary key
    /// already carries <c>posting_date</c> because a partitioned table requires the
    /// partition key in every unique constraint.
    /// </remarks>
    public const string PartitionJournalLines = """
        drop table if exists ledger.journal_lines cascade;

        create table ledger.journal_lines (
            line_id          uuid           not null,
            journal_id       uuid           not null,
            account_id       uuid           not null,
            debit_or_credit  text           not null,
            amount           numeric(23,4)  not null,
            currency         char(3)        not null,
            sequence_number  integer        not null,
            posting_date     date           not null,
            constraint pk_journal_lines primary key (line_id, posting_date),
            constraint fk_journal_lines_entry foreign key (journal_id)
                references ledger.journal_entries (journal_id),
            constraint ck_journal_lines_amount_positive check (amount > 0),
            constraint ck_journal_lines_direction check (debit_or_credit in ('debit', 'credit'))
        ) partition by range (posting_date);

        create index ix_journal_lines_journal_id on ledger.journal_lines (journal_id);
        create index ix_journal_lines_account_id on ledger.journal_lines (account_id);
        """;

    /// <summary>
    /// The balances projection (I-6).
    /// </summary>
    /// <remarks>
    /// Created here rather than by EF because it has no aggregate: balances are
    /// derived, fully rebuildable from <c>journal_lines</c> alone, and **nothing
    /// writes to this table until S05**. The table exists now because the slice's
    /// build says all five tables; the projection that fills it is a later slice.
    /// </remarks>
    public const string BalancesTable = """
        create table if not exists ledger.balances (
            account_id         uuid           not null references ledger.accounts (account_id),
            as_of_date         date           not null,
            opening_balance    numeric(23,4)  not null default 0,
            closing_balance    numeric(23,4)  not null default 0,
            available_balance  numeric(23,4)  not null default 0,
            ledger_balance     numeric(23,4)  not null default 0,
            constraint pk_balances primary key (account_id, as_of_date)
        );
        """;

    /// <summary>I-2 at rest: every line shares its entry's currency.</summary>
    public const string CurrencyMatchesEntry = """
        create or replace function ledger.assert_line_currency_matches_entry()
        returns trigger language plpgsql as $$
        declare entry_currency char(3);
        begin
            select currency into entry_currency
            from ledger.journal_entries where journal_id = new.journal_id;

            if entry_currency is distinct from new.currency then
                raise exception
                    'Line currency % does not match entry currency % (I-2).',
                    new.currency, entry_currency;
            end if;

            return new;
        end $$;

        create trigger trg_line_currency_matches_entry
        before insert or update on ledger.journal_lines
        for each row execute function ledger.assert_line_currency_matches_entry();
        """;

    /// <summary>
    /// I-1 at rest, checked at COMMIT rather than per row.
    /// </summary>
    /// <remarks>
    /// Deferred on purpose: an entry is only balanced once every line is in, so a
    /// per-statement check would reject the first line of a perfectly good entry.
    /// </remarks>
    public const string DeferredBalanceCheck = """
        -- The tokens below must match what the column actually stores, lowercase per
        -- ledger-spec.md §2. A mismatch here does not error: the filters match nothing,
        -- both sums come back 0, 0 = 0, and the trigger waves through every unbalanced
        -- entry it was written to stop. That is how this was found — by a test that
        -- inserts an unbalanced entry in raw SQL and demands a refusal.
        create or replace function ledger.assert_entry_balanced()
        returns trigger language plpgsql as $$
        declare
            target uuid := coalesce(new.journal_id, old.journal_id);
            debits numeric(23,4);
            credits numeric(23,4);
            lines integer;
        begin
            select
                coalesce(sum(amount) filter (where debit_or_credit = 'debit'), 0),
                coalesce(sum(amount) filter (where debit_or_credit = 'credit'), 0),
                count(*)
            into debits, credits, lines
            from ledger.journal_lines where journal_id = target;

            if lines = 0 then
                return null;
            end if;

            -- Unreachable today and kept deliberately: a single line is always
            -- unbalanced, so the check below refuses it first, and mutation testing
            -- confirms disabling this branch changes no test. It is defence in depth,
            -- not a tested rule — do not read its presence as coverage.
            if lines < 2 then
                raise exception 'Entry % has % line(s); double entry needs at least two.',
                    target, lines;
            end if;

            if debits <> credits then
                raise exception 'Entry % is unbalanced: debits %, credits % (I-1).',
                    target, debits, credits;
            end if;

            return null;
        end $$;

        create constraint trigger trg_entry_balanced
        after insert or update or delete on ledger.journal_lines
        deferrable initially deferred
        for each row execute function ledger.assert_entry_balanced();
        """;

    /// <summary>
    /// I-3 and D-10: corrections are new entries, never edits.
    /// </summary>
    /// <remarks>
    /// A trigger rather than only a grant, because grants do not bind the table's
    /// owner. The single permitted change is an entry advancing forward through its
    /// lifecycle and being linked to its reversal once — everything else raises.
    /// </remarks>
    public const string AppendOnly = """
        create or replace function ledger.refuse_mutation()
        returns trigger language plpgsql as $$
        begin
            if tg_op = 'DELETE' then
                raise exception
                    'ledger.% is append-only: a correction is a new entry, never a delete (I-3, D-10).',
                    tg_table_name;
            end if;

            if tg_table_name = 'journal_entries' then
                if new.journal_id is distinct from old.journal_id
                   or new.transaction_date is distinct from old.transaction_date
                   or new.value_date is distinct from old.value_date
                   or new.posting_date is distinct from old.posting_date
                   or new.description is distinct from old.description
                   or new.source_system is distinct from old.source_system
                   or new.source_reference is distinct from old.source_reference
                   or new.currency is distinct from old.currency
                   or new.created_by is distinct from old.created_by
                then
                    raise exception
                        'ledger.journal_entries is append-only: only status and reversal linkage may advance (I-3).';
                end if;

                if old.status = 'reversed' and new.status <> old.status then
                    raise exception 'Entry % is already reversed; status is forward-only (I-3).', old.journal_id;
                end if;

                return new;
            end if;

            raise exception
                'ledger.% is append-only: a correction is a new entry, never an update (I-3, D-10).',
                tg_table_name;
        end $$;

        create trigger trg_journal_entries_append_only
        before update or delete on ledger.journal_entries
        for each row execute function ledger.refuse_mutation();

        create trigger trg_journal_lines_append_only
        before update or delete on ledger.journal_lines
        for each row execute function ledger.refuse_mutation();
        """;

    /// <summary>
    /// The least-privileged role the application connects as (I-3, I-7).
    /// </summary>
    /// <remarks>
    /// Defence in depth beside the triggers: the grant stops the application, the
    /// trigger stops everyone including the owner. Revoking from the owner alone
    /// would be theatre — an owner keeps its rights.
    /// </remarks>
    /// <summary>Creates the partition the database needs to accept its first posting.</summary>
    /// <remarks>
    /// Without this a freshly migrated database is partitioned with **no partitions**,
    /// and every insert fails with "no partition of relation journal_lines found for
    /// row" — a ledger that refuses all money from the moment it is deployed. The
    /// integration suite caught exactly that.
    /// <para>
    /// This is a bootstrap, not a policy: one month, computed at deploy time, so the
    /// database works the instant it exists. How far ahead partitions are kept
    /// thereafter is <c>PartitionOptions.MonthsAhead</c> and belongs to the running
    /// system, not to a migration frozen at the moment it was written.
    /// </para>
    /// </remarks>
    public const string BootstrapCurrentPartition = """
        do $$
        declare
            month_start date := date_trunc('month', now())::date;
        begin
            execute format(
                'create table if not exists ledger.%I partition of ledger.journal_lines
                 for values from (%L) to (%L)',
                'journal_lines_' || to_char(month_start, 'YYYY_MM'),
                month_start,
                month_start + interval '1 month');
        end $$;
        """;

    public const string ApplicationRoleGrants = """
        do $$ begin
            if not exists (select 1 from pg_roles where rolname = 'sirara_ledger_app') then
                create role sirara_ledger_app;
            end if;
        end $$;

        grant usage on schema ledger to sirara_ledger_app;
        grant select, insert on all tables in schema ledger to sirara_ledger_app;
        revoke update, delete, truncate on all tables in schema ledger from sirara_ledger_app;

        alter default privileges in schema ledger
            grant select, insert on tables to sirara_ledger_app;
        """;
}
