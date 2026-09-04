using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class ThrowingDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("boom");
}
