using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Money : ValueObject
{
    // Brut, sans validation : c'est lui la réhydratation — EF le lie pour les
    // colonnes owned (amount, currency), et les opérations internes le
    // réutilisent, la devise étant déjà prouvée (ADR 0016).
    private Money(
        decimal amount,
        string currency
    )
    {
        Amount = amount;
        Currency = currency;
    }

    // Création : la devise vient d'une frontière, elle se valide ici — aucune
    // instance invalide ne peut être créée.
    public static Money Of(
        decimal amount,
        string currency
    )
    {
        if (!CurrencyFormat().IsMatch(currency)) {
            throw new InvalidCurrencyException(currency);
        }

        return new Money(
            amount: amount,
            currency: currency
        );
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
