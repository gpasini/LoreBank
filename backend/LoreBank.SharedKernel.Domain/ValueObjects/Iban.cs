using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Iban : SimpleValueObject<string>
{
    private Iban(string value) : base(value)
    {
    }

    // Création : l'entrée vient d'une frontière — normaliser d'abord, valider
    // ensuite, aucune instance invalide ne peut être créée.
    public static Iban Parse(string value)
    {
        var normalized = Normalize(value);

        if (!Format().IsMatch(normalized)) {
            throw new InvalidIbanException(value);
        }

        return new Iban(normalized);
    }

    // Réhydratation : la valeur vient de la base, écrite par Parse et
    // maintenue par les data migrations — relire n'est pas re-décider
    // (ADR 0016). Jamais appelé depuis du code métier.
    public static Iban Hydrate(string value) => new(value);

    private static string Normalize(string value) => value
        .Replace(
            oldValue: " ",
            newValue: string.Empty
        )
        .ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex Format();
}
