namespace LoreBank.SharedKernel.Domain.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    );
}
