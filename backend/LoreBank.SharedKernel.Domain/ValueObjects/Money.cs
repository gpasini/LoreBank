using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Money : ValueObject
{
    public Money(
        decimal amount,
        string currency
    )
    {
        if (!CurrencyFormat().IsMatch(currency)) {
            throw new InvalidCurrencyException(currency);
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public Money Add(Money other) => new(
        amount: Amount + EnsureSameCurrency(other).Amount,
        currency: Currency
    );

    public Money Subtract(Money other) => new(
        amount: Amount - EnsureSameCurrency(other).Amount,
        currency: Currency
    );

    public static Money operator +(
        Money left,
        Money right
    ) => left.Add(right);

    public static Money operator -(
        Money left,
        Money right
    ) => left.Subtract(right);

    protected override IEnumerable<object?> GetEqualityComponents() => [Amount, Currency];

    public override string ToString() => $"{Amount} {Currency}";

    private Money EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency) {
            throw new CurrencyMismatchException(
                left: Currency,
                right: other.Currency
            );
        }

        return other;
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyFormat();
}
