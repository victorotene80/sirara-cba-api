namespace SiraraCBA.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Publishes an event to other modules through this module's outbox.
/// </summary>
/// <remarks>
/// The event is written to the outbox inside the current transaction and dispatched
/// afterwards, so it is never published for a change that rolled back and never
/// lost for one that committed. Delivery is at-least-once from day one, because
/// that is what a broker gives once the module is extracted — handlers are
/// idempotent (ER-8).
/// </remarks>
public interface IIntegrationEventPublisher
{
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// A fact one module publishes for others to react to. Part of the publishing
/// module's public contract, and versioned as carefully as any other API.
/// </summary>
public interface IIntegrationEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Stable wire name, e.g. <c>ledger.journal-entry.posted.v1</c>. Never a CLR
    /// type name: renaming a class must not break a subscriber, and an
    /// assembly-qualified name is an unsafe deserialisation target.
    /// </summary>
    public string EventType { get; }
}
