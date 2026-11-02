using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiraraCBA.Modules.Ledger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedgerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "ledger",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_number = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_type = table.Column<string>(type: "text", nullable: false),
                    gl_code = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "chart_of_accounts",
                schema: "ledger",
                columns: table => new
                {
                    gl_code = table.Column<string>(type: "text", nullable: false),
                    gl_name = table.Column<string>(type: "text", nullable: false),
                    account_type = table.Column<string>(type: "text", nullable: false),
                    normal_balance = table.Column<string>(type: "text", nullable: false),
                    parent_gl_code = table.Column<string>(type: "text", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_postable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chart_of_accounts", x => x.gl_code);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "ledger",
                columns: table => new
                {
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    value_date = table.Column<DateOnly>(type: "date", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    source_system = table.Column<string>(type: "text", nullable: false),
                    source_reference = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.journal_id);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
                schema: "ledger",
                columns: table => new
                {
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debit_or_credit = table.Column<string>(type: "text", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(23,4)", precision: 23, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_lines", x => new { x.line_id, x.posting_date });
                    table.ForeignKey(
                        name: "FK_journal_lines_journal_entries_journal_id",
                        column: x => x.journal_id,
                        principalSchema: "ledger",
                        principalTable: "journal_entries",
                        principalColumn: "journal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_account_number",
                schema: "ledger",
                table: "accounts",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_source_system_source_reference",
                schema: "ledger",
                table: "journal_entries",
                columns: new[] { "source_system", "source_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_journal_id",
                schema: "ledger",
                table: "journal_lines",
                column: "journal_id");

            // Everything EF Core cannot express. Order matters: the partitioned table
            // must exist before triggers reference it, and grants come last so they
            // cover what was just created.
            migrationBuilder.Sql(LedgerHardening.PartitionJournalLines);
            migrationBuilder.Sql(LedgerHardening.BootstrapCurrentPartition);
            migrationBuilder.Sql(LedgerHardening.BalancesTable);
            migrationBuilder.Sql(LedgerHardening.CurrencyMatchesEntry);
            migrationBuilder.Sql(LedgerHardening.DeferredBalanceCheck);
            migrationBuilder.Sql(LedgerHardening.AppendOnly);
            migrationBuilder.Sql(LedgerHardening.ApplicationRoleGrants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "chart_of_accounts",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "journal_lines",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "ledger");
        }
    }
}
