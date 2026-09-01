using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class ThrowingDomainEventHandler : IDomainEventHandler<SomethingHappened>
{
    public Task HandleAsync(
        SomethingHappened domainEvent,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("boom");
}
