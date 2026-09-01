using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.SharedKernel.Domain.Entities;

public abstract class Entity<TId>(TId id) : IHasDomainEvents where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = id;

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) {
            return false;
        }

        if (GetType() != other.GetType()) {
            return false;
        }

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(
        value1: GetType(),
        value2: Id
    );

    public static bool operator ==(
        Entity<TId>? left,
        Entity<TId>? right
    ) => Equals(
        objA: left,
        objB: right
    );

    public static bool operator !=(
        Entity<TId>? left,
        Entity<TId>? right
    ) => !Equals(
        objA: left,
        objB: right
    );
}
