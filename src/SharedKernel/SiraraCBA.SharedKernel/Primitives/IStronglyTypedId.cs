namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// A wrapped identifier. Modules declare their own (<c>AccountId</c>,
/// <c>JournalId</c>) so an id of one kind can never be passed where another is
/// expected — a raw <see cref="Guid"/> parameter is a bug waiting for the wrong
/// argument order (coding-standards.md, Domain).
/// </summary>
public interface IStronglyTypedId
{
    public Guid Value { get; }
}
