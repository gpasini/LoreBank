namespace LoreBank.SharedKernel.Domain.ValueObjects;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is ValueObject other
        && other.GetType() == GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode() => GetEqualityComponents().Aggregate(
        seed: GetType().GetHashCode(),
        func: HashCode.Combine
    );

    public static bool operator ==(
        ValueObject? left,
        ValueObject? right
    ) => Equals(
        objA: left,
        objB: right
    );

    public static bool operator !=(
        ValueObject? left,
        ValueObject? right
    ) => !Equals(
        objA: left,
        objB: right
    );
}
