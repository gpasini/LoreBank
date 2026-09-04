using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

// Le montant d'une opération : strictement positif. La garde ne peut pas
// vivre dans Money — un solde négatif doit rester représentable — mais une
// transition qui prend un Money nu accepte un dépôt négatif qui débite en
// contournant les contrôles de retrait. Prendre ce type en paramètre rend le
// cas inexprimable. La devise est déléguée au Money interne : pas de seconde
// règle de validation.
public sealed class PositiveMoney : ValueObject
{
    public PositiveMoney(
        decimal amount,
        string currency
    )
    {
        if (amount <= 0m) {
            throw new NonPositiveAmountException(
                amount: amount,
                currency: currency
            );
        }

        Value = new Money(
            amount: amount,
            currency: currency
        );
    }

    public Money Value { get; }

    protected override IEnumerable<object?> GetEqualityComponents() => [Value];

    public override string ToString() => Value.ToString();
}
