using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class ThrowingAfterAwaitDomainEventHandler : IDomainEventHandler<SomethingHappened>
{
    public async Task HandleAsync(
        SomethingHappened domainEvent,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        throw new InvalidOperationException("boom after await");
    }
}
