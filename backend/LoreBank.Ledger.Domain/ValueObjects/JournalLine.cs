using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.ValueObjects;

// Une jambe d'écriture : un compte, un sens, un montant strictement positif —
// c'est le sens (débit/crédit) qui porte la direction, jamais le signe du
// montant. La factory exige un PositiveMoney (une jambe nulle ou négative est
// inexprimable) mais la valeur stockée est le Money qu'il enveloppe — c'est
// lui qu'EF sait matérialiser — recopié dans une instance propre : un Money
// owned n'a droit qu'à un propriétaire, deux jambes bâties sur le même
// PositiveMoney partageraient la leur et la seconde partirait en NULL à
// l'écriture.
public sealed class JournalLine : ValueObject
{
    private JournalLine(
        LedgerAccountRef account,
        JournalDirection direction,
        Money amount
    )
    {
        Account = account;
        Direction = direction;
        Amount = amount;
    }

    // Réservé à la matérialisation EF Core (type owned), qui écrit ensuite les
    // backing fields.
    private JournalLine()
    {
        Account = null!;
        Amount = null!;
    }

    public static JournalLine Of(
        LedgerAccountRef account,
        JournalDirection direction,
        PositiveMoney amount
    ) => new(
        account: account,
        direction: direction,
        amount: Money.Of(
            amount: amount.Value.Amount,
            currency: amount.Value.Currency
        )
    );

    public LedgerAccountRef Account { get; }

    public JournalDirection Direction { get; }

    public Money Amount { get; }

    protected override IEnumerable<object?> GetEqualityComponents() => [Account, Direction, Amount];
}
