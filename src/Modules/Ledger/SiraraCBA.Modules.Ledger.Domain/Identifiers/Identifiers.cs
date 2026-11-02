namespace SiraraCBA.Modules.Ledger.Domain.Identifiers;

/// <summary>Identifies an account. Never a bare <see cref="Guid"/> (ER-7, coding-standards.md).</summary>
public readonly record struct AccountId(Guid Value) : IStronglyTypedId;

/// <summary>Identifies a journal entry.</summary>
public readonly record struct JournalId(Guid Value) : IStronglyTypedId;

/// <summary>Identifies one line of a journal entry.</summary>
public readonly record struct JournalLineId(Guid Value) : IStronglyTypedId;

/// <summary>A general-ledger code from the chart of accounts.</summary>
public readonly record struct GlCode(string Value);

/// <summary>An account's externally visible number.</summary>
public readonly record struct AccountNumber(string Value);
