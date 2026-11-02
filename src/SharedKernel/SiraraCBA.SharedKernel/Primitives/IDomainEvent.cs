namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// Something that happened inside one aggregate, dispatched within the same unit
/// of work. Not an integration event — those cross module boundaries through the
/// outbox (architecture.md §5).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identifies this occurrence. Supplied by the raiser, never generated inline.</summary>
    public Guid EventId { get; }

    /// <summary>When it happened, from <c>IClock</c>. Never <c>DateTime.UtcNow</c> (ER-7).</summary>
    public DateTimeOffset OccurredAt { get; }
}
