using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// L'agrégat-terrain du socle : un champ propre (Name) et un VO owned (Price),
// les deux géométries que la Version d'agrégat doit couvrir — modifier la
// racine, ou seulement remplacer l'instance d'un VO owned.
public sealed class TestThing(Guid id) : AggregateRoot<Guid>(id)
{
    public string Name { get; private set; } = string.Empty;

    public Money? Price { get; private set; }

    public void Rename(string name)
    {
        Name = name;
    }

    public void Reprice(Money price)
    {
        Price = price;
    }

    public void Happen()
    {
        AddDomainEvent(new SomethingHappenedDomainEvent());
    }
}
