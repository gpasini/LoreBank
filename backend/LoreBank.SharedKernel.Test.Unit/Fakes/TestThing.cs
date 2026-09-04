using LoreBank.SharedKernel.Domain.Entities;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestThing(Guid id) : Entity<Guid>(id)
{
    public void Happen()
    {
        AddDomainEvent(new SomethingHappened());
    }
}
