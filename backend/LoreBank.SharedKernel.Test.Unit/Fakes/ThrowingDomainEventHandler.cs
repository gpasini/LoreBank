using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class ThrowingDomainEventHandler : IDomainEventHandler<SomethingHappenedDomainEvent>
{
    public Task HandleAsync(
        SomethingHappenedDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("boom");
}
