using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Domain.Entities;

// Entity<TId> est générique : sans cette interface, le ChangeTracker d'EF n'a
// aucun type commun à interroger pour ramasser les events.
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
