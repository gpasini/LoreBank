namespace LoreBank.SharedKernel.Domain.ValueObjects;

// Protected : instancier un VO est réservé à ses factories — la création
// nommée qui valide et normalise, et Hydrate qui truste la base (ADR 0016).
// Un constructeur public rendrait le geste ambigu, et DomainConventionTest
// l'interdit.
public abstract class SimpleValueObject<TValue> : ValueObject where TValue : notnull
{
    protected SimpleValueObject(TValue value)
    {
        Value = value;
    }

    public TValue Value { get; }

    protected override IEnumerable<object?> GetEqualityComponents() => [Value];

    public override string ToString() => Value.ToString() ?? string.Empty;
}
