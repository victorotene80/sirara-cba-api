using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SiraraCBA.Modules.Ledger.Domain.Accounts;
using SiraraCBA.Modules.Ledger.Domain.ChartOfAccounts;
using SiraraCBA.Modules.Ledger.Domain.Identifiers;
using SiraraCBA.Modules.Ledger.Domain.Journals;

namespace SiraraCBA.Modules.Ledger.Infrastructure.Persistence.Configurations;

/// <summary>
/// Converters shared by the entity configurations.
/// </summary>
/// <remarks>
/// The currency converter is built from <see cref="ICurrencyRegistry"/> rather than
/// from <c>new Currency(code)</c>: minor units come from the registry and never from
/// a literal (ER-9), so rehydration has to go through it too. That is why the
/// <see cref="LedgerDbContext"/> takes a registry.
/// </remarks>
internal static class LedgerConverters
{
    /// <summary>
    /// Stores an enum as the lowercase token `ledger-spec.md` §2 specifies.
    /// </summary>
    /// <remarks>
    /// EF's default <c>HasConversion&lt;string&gt;()</c> writes the member name, which
    /// is PascalCase — so the schema said <c>'Debit'</c> while the spec says
    /// <c>debit</c>. The spec is the contract and `context/` is read-only, so the
    /// conversion moves rather than the specification. Casing matters here because
    /// these columns are read by SQL that is not ours: a report, a migration, a
    /// downstream extract.
    /// </remarks>
    public static ValueConverter<TEnum, string> LowercaseEnum<TEnum>()
        where TEnum : struct, Enum =>
        new(value => value.ToString().ToLowerInvariant(),
            stored => Enum.Parse<TEnum>(stored, ignoreCase: true));

    public static ValueConverter<Currency, string> Currency(ICurrencyRegistry registry) =>
        new(currency => currency.Code,
            code => registry.Get(code).Value);

    public static ValueConverter<GlCode, string> GlCode { get; } =
        new(code => code.Value, value => new GlCode(value));

    public static ValueConverter<AccountNumber, string> AccountNumber { get; } =
        new(number => number.Value, value => new AccountNumber(value));

    public static ValueConverter<AccountId, Guid> AccountId { get; } =
        new(id => id.Value, value => new AccountId(value));

    public static ValueConverter<JournalId, Guid> JournalId { get; } =
        new(id => id.Value, value => new JournalId(value));

    public static ValueConverter<JournalLineId, Guid> JournalLineId { get; } =
        new(id => id.Value, value => new JournalLineId(value));
}

internal sealed class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
    {
        builder.ToTable(LedgerSchema.ChartOfAccounts, LedgerSchema.Name);
        builder.HasKey(node => node.Id);

        builder.Property(node => node.Id).HasColumnName("gl_code").HasConversion(LedgerConverters.GlCode);
        builder.Property(node => node.Name).HasColumnName("gl_name").IsRequired();
        builder.Property(node => node.Type).HasColumnName("account_type").HasConversion(LedgerConverters.LowercaseEnum<AccountType>()).IsRequired();
        builder.Property(node => node.NormalBalance).HasColumnName("normal_balance").HasConversion(LedgerConverters.LowercaseEnum<NormalBalance>()).IsRequired();
        builder.Property(node => node.ParentGlCode).HasColumnName("parent_gl_code").HasConversion(LedgerConverters.GlCode);
        builder.Property(node => node.Level).HasColumnName("level");
        builder.Property(node => node.IsPostable).HasColumnName("is_postable");
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    private readonly ICurrencyRegistry _currencies;

    public AccountConfiguration(ICurrencyRegistry currencies) => _currencies = currencies;

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(LedgerSchema.Accounts, LedgerSchema.Name);
        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id).HasColumnName("account_id").HasConversion(LedgerConverters.AccountId);
        builder.Property(account => account.Number).HasColumnName("account_number").HasConversion(LedgerConverters.AccountNumber).IsRequired();

        // Opaque. No foreign key to any Customers module (ER-2, O-4).
        builder.Property(account => account.CustomerId).HasColumnName("customer_id");
        builder.Property(account => account.ProductType).HasColumnName("product_type").IsRequired();
        builder.Property(account => account.GlCode).HasColumnName("gl_code").HasConversion(LedgerConverters.GlCode).IsRequired();
        builder.Property(account => account.Currency).HasColumnName("currency")
            .HasConversion(LedgerConverters.Currency(_currencies)).HasMaxLength(CurrencyCodeLength).IsRequired();
        builder.Property(account => account.Status).HasColumnName("status").HasConversion(LedgerConverters.LowercaseEnum<AccountStatus>()).IsRequired();
        builder.Property(account => account.OpenedAt).HasColumnName("opened_at");

        builder.HasIndex(account => account.Number).IsUnique();
    }

    /// <summary>ISO 4217 alphabetic codes are three characters. A format, not a policy.</summary>
    internal const int CurrencyCodeLength = 3;
}

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    private readonly ICurrencyRegistry _currencies;

    public JournalEntryConfiguration(ICurrencyRegistry currencies) => _currencies = currencies;

    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable(LedgerSchema.JournalEntries, LedgerSchema.Name);
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("journal_id").HasConversion(LedgerConverters.JournalId);
        builder.Property(entry => entry.TransactionDate).HasColumnName("transaction_date");
        builder.Property(entry => entry.ValueDate).HasColumnName("value_date");
        builder.Property(entry => entry.PostingDate).HasColumnName("posting_date");
        builder.Property(entry => entry.Description).HasColumnName("description").IsRequired();
        builder.Property(entry => entry.SourceSystem).HasColumnName("source_system").IsRequired();
        builder.Property(entry => entry.SourceReference).HasColumnName("source_reference").IsRequired();
        builder.Property(entry => entry.Currency).HasColumnName("currency")
            .HasConversion(LedgerConverters.Currency(_currencies)).HasMaxLength(AccountConfiguration.CurrencyCodeLength).IsRequired();
        builder.Property(entry => entry.Status).HasColumnName("status").HasConversion(LedgerConverters.LowercaseEnum<JournalStatus>()).IsRequired();
        builder.Property(entry => entry.CreatedBy).HasColumnName("created_by");

        // created_at exists in the schema but not on the aggregate: it is a
        // persistence fact, and the domain never reads a clock (ER-7).
        builder.Property<DateTimeOffset>("created_at").HasColumnName("created_at").HasDefaultValueSql("now()");

        // I-5. Idempotency is a database guarantee, not a race the application hopes
        // to win.
        builder.HasIndex(entry => new { entry.SourceSystem, entry.SourceReference }).IsUnique();

        builder.HasMany(entry => entry.Lines)
            .WithOne()
            .HasForeignKey("journal_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(entry => entry.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    private readonly ICurrencyRegistry _currencies;

    public JournalLineConfiguration(ICurrencyRegistry currencies) => _currencies = currencies;

    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable(LedgerSchema.JournalLines, LedgerSchema.Name);

        // Denormalised from the entry — required as the partition key.
        builder.Property<DateOnly>("posting_date").HasColumnName("posting_date");

        // PostgreSQL requires the partition key in the primary key (O-5).
        builder.HasKey("Id", "posting_date");

        builder.Property(line => line.Id).HasColumnName("line_id").HasConversion(LedgerConverters.JournalLineId);
        builder.Property<JournalId>("journal_id").HasColumnName("journal_id")
            .HasConversion(LedgerConverters.JournalId);
        builder.Property(line => line.AccountId).HasColumnName("account_id").HasConversion(LedgerConverters.AccountId);
        builder.Property(line => line.Direction).HasColumnName("debit_or_credit").HasConversion(LedgerConverters.LowercaseEnum<DebitOrCredit>()).IsRequired();
        builder.Property(line => line.SequenceNumber).HasColumnName("sequence_number");

        builder.ComplexProperty(line => line.Amount, money =>
        {
            money.Property(value => value.Amount).HasColumnName("amount").HasPrecision(AmountPrecision, AmountScale);
            money.Property(value => value.Currency).HasColumnName("currency")
                .HasConversion(LedgerConverters.Currency(_currencies))
                .HasMaxLength(AccountConfiguration.CurrencyCodeLength);
        });
    }

    /// <summary>numeric(23,4) — the storage type ledger-spec.md §2 names, not a business limit.</summary>
    private const int AmountPrecision = 23;
    private const int AmountScale = 4;
}
