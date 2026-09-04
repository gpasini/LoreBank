using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Infrastructure.Events;

// Le dispatcher des migrations de données (ADR 0013) : une migration
// re-représente des faits métier déjà établis — leurs events ont déjà eu
// lieu, et les redispatcher déclencherait les effets de bord (courriers,
// notifications) une seconde fois, multipliés par le volume migré.
// DataMigrationRunner le substitue dans son scope ; il n'a aucun autre usage
// légitime.
public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}
