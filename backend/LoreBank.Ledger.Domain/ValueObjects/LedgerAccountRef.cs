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

    public LedgerAccountRef(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();

        if (!RefFormat().IsMatch(normalized)) {
            throw new InvalidLedgerAccountRefException(value);
        }

        Value = normalized;
    }

    // La trésorerie : le compte technique en contrepartie de tout mouvement.
    public static LedgerAccountRef Cash { get; } = new("CASH");

    public static LedgerAccountRef ForBankAccount(Guid bankAccountId) =>
        new($"{BankAccountPrefix}{bankAccountId:D}");

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
