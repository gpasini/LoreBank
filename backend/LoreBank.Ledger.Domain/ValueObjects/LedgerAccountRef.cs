using System.Text.RegularExpressions;
using LoreBank.Ledger.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.ValueObjects;

// La référence de compte d'une ligne d'écriture : soit un compte technique
// nommé (la trésorerie, « CASH »), soit le compte d'un client — identifié par
// l'id de son compte Bank, la seule chose que le Ledger sait de lui. Pas
// d'agrégat plan comptable (décision de cadrage) : la référence est une
// valeur, le module est un consommateur exemplaire, pas un logiciel de
// comptabilité.
public sealed partial class LedgerAccountRef : ValueObject
{
    private const string BankAccountPrefix = "BANK:";

    private LedgerAccountRef(string value)
    {
        Value = value;
    }

    // La trésorerie : le compte technique en contrepartie de tout mouvement.
    public static LedgerAccountRef Cash { get; } = new("CASH");

    // Correct par construction : la forme canonique est fabriquée ici, rien à
    // valider.
    public static LedgerAccountRef ForBankAccount(Guid bankAccountId) =>
        new($"{BankAccountPrefix}{bankAccountId:D}".ToUpperInvariant());

    // Création depuis une chaîne venue d'une frontière : normalise puis
    // valide — c'est le gardien publié de la forme canonique.
    public static LedgerAccountRef Parse(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();

        if (!RefFormat().IsMatch(normalized)) {
            throw new InvalidLedgerAccountRefException(value);
        }

        return new LedgerAccountRef(normalized);
    }

    // Réhydratation : truste la base (ADR 0016). Jamais appelé depuis du
    // code métier.
    public static LedgerAccountRef Hydrate(string value) => new(value);

    public string Value { get; }

    public bool IsBankAccount => Value.StartsWith(
        value: BankAccountPrefix,
        comparisonType: StringComparison.Ordinal
    );

    protected override IEnumerable<object?> GetEqualityComponents() => [Value];

    public override string ToString() => Value;

    [GeneratedRegex("^(CASH|BANK:[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12})$")]
    private static partial Regex RefFormat();
}
