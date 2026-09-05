using LoreBank.SharedKernel.Domain.Aggregates;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestThing(Guid id) : AggregateRoot<Guid>(id)
{
    public void Happen()
    {
        AddDomainEvent(new SomethingHappenedDomainEvent());
    }
}
