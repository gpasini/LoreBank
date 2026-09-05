using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.ValueObjects;

// Une jambe d'écriture : un compte, un sens, un montant strictement positif —
// c'est le sens (débit/crédit) qui porte la direction, jamais le signe du
// montant. Le constructeur exige un PositiveMoney (une jambe nulle ou
// négative est inexprimable) mais la valeur stockée est le Money qu'il
// enveloppe : c'est lui qu'EF sait matérialiser, et la garantie de
// construction suffit — la matérialisation ne repasse par aucune factory,
// ici comme partout.
public sealed class JournalLine : ValueObject
{
    public JournalLine(
        LedgerAccountRef account,
        JournalDirection direction,
        PositiveMoney amount
    )
    {
        Account = account;
        Direction = direction;
        // Copie défensive : le Money est mappé en type owned, et EF n'accepte
        // une instance possédée que chez un seul propriétaire — deux jambes
        // bâties sur le même PositiveMoney partageraient la leur, et la
        // seconde partirait en NULL à l'écriture.
        Amount = new Money(
            amount: amount.Value.Amount,
            currency: amount.Value.Currency
        );
    }

    // Réservé à la matérialisation EF Core (type owned), qui écrit ensuite les
    // backing fields.
    private JournalLine()
    {
        Account = null!;
        Amount = null!;
    }

    public LedgerAccountRef Account { get; }

    public JournalDirection Direction { get; }

    public Money Amount { get; }

    protected override IEnumerable<object?> GetEqualityComponents() => [Account, Direction, Amount];
}
