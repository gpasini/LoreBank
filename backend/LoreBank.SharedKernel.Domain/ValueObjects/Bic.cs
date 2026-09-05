using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Bic : SimpleValueObject<string>
{
    private Bic(string value) : base(value)
    {
    }

    public static Bic Parse(string value)
    {
        var normalized = Normalize(value);

        if (!Format().IsMatch(normalized)) {
            throw new InvalidBicException(value);
        }

        return new Bic(normalized);
    }

    // Réhydratation : truste la base (ADR 0016). Jamais appelé depuis du
    // code métier.
    public static Bic Hydrate(string value) => new(value);

    private static string Normalize(string value) => value
        .Replace(
            oldValue: " ",
            newValue: string.Empty
        )
        .ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex Format();
}
