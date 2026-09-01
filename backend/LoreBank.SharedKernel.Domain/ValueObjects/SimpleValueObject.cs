namespace LoreBank.SharedKernel.Domain.ValueObjects;

public abstract class SimpleValueObject<TValue>(TValue value) : ValueObject where TValue : notnull
{
    public TValue Value { get; } = value;

    protected override IEnumerable<object?> GetEqualityComponents() => [Value];

    public override string ToString() => Value.ToString() ?? string.Empty;
}
