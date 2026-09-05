using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Aggregates;

public sealed class BankAccountId : SimpleValueObject<Guid>
{
    private BankAccountId(Guid value) : base(value)
    {
    }

    public static BankAccountId New() => new(Guid.NewGuid());

    // Un Guid n'a pas de règle à valider : réhydrater couvre aussi bien la
    // colonne lue que l'id reçu d'une commande (ADR 0016).
    public static BankAccountId Hydrate(Guid value) => new(value);
}
