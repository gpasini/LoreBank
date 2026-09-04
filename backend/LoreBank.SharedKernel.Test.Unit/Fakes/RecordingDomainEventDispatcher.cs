using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly List<IDomainEvent> _dispatched = [];

    public IReadOnlyList<IDomainEvent> Dispatched => _dispatched;

    // Observateur optionnel, appelé au moment du dispatch : permet d'inspecter
    // l'état de la base pendant que SaveChangesAsync est encore en cours.
    public Func<Task>? OnDispatchAsync { get; set; }

    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    )
    {
        _dispatched.AddRange(domainEvents);

        if (OnDispatchAsync is not null) {
            await OnDispatchAsync();
        }
    }
}
