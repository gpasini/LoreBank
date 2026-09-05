using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class ThrowingAfterAwaitDomainEventHandler : IDomainEventHandler<SomethingHappenedDomainEvent>
{
    public async Task HandleAsync(
        SomethingHappenedDomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        throw new InvalidOperationException("boom after await");
    }
}
