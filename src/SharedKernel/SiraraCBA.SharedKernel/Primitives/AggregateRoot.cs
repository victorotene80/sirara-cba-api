namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// The consistency boundary. Invariants are enforced here — in constructors and
/// factory methods — so an invalid instance cannot be constructed at all
/// (coding-standards.md, Domain).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>
    /// Events raised but not yet dispatched. The unit of work dispatches and
    /// clears them; a handler never publishes directly.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
