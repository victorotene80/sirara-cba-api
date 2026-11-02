namespace SiraraCBA.SharedKernel.UnitTests.Primitives;

public sealed class AggregateRootTests
{
    [Fact]
    public void RaisedEvents_AreHeldUntilTheUnitOfWorkClearsThem()
    {
        var aggregate = new TestAggregate(Guid.CreateVersion7());

        aggregate.DoSomething(new TestEvent(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch));

        aggregate.DomainEvents.Count.ShouldBe(1);

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Entities_AreComparedByIdentity_NotByReference()
    {
        Guid id = Guid.CreateVersion7();

        new TestAggregate(id).ShouldBe(new TestAggregate(id));
        new TestAggregate(id).ShouldNotBe(new TestAggregate(Guid.CreateVersion7()));
    }

    private sealed record TestEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id)
            : base(id)
        {
        }

        public void DoSomething(IDomainEvent domainEvent) => Raise(domainEvent);
    }
}
