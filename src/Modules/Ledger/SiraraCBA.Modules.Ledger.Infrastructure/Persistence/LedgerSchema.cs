using Microsoft.EntityFrameworkCore.Migrations;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence;

/// <summary>
/// The one place this module's schema and table names are written.
/// </summary>
/// <remarks>
/// One schema per module, and no object crosses a schema boundary (ER-2). Naming it
/// once means a migration, a trigger and a grant cannot drift apart, and it is what
/// the architecture rule reads when it checks that nothing here names
/// <c>identity</c> or <c>approvals</c>.
/// </remarks>
public static class LedgerSchema
{
    public const string Name = "ledger";

    public const string ChartOfAccounts = "chart_of_accounts";
    public const string Accounts = "accounts";
    public const string JournalEntries = "journal_entries";
    public const string JournalLines = "journal_lines";
    public const string Balances = "balances";

    /// <summary>EF's migrations history, kept in this schema rather than EF's default <c>public</c>.</summary>
    public const string MigrationsHistory = HistoryRepository.DefaultTableName;

    /// <summary>The least-privileged role the application connects as.</summary>
    /// <remarks>
    /// Distinct from the migration owner on purpose: revoking UPDATE from an owner
    /// achieves nothing, because an owner keeps its rights. The grants below are only
    /// meaningful against a role that is not the owner (I-3, D-10).
    /// </remarks>
    public const string ApplicationRole = "sirara_ledger_app";
}
