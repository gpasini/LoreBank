using System.Globalization;

namespace LoreBank.SharedKernel.Application;

// Une Facette : les valeurs présentes d'un filtre et leur compte, comptées
// hors de son propre filtre — le compte dit ce que cocher cette valeur
// donnerait (ADR 0027). Son nom est celui du paramètre de filtre qu'elle
// alimente, en camelCase comme les clés JSON : le front lie l'un à l'autre
// sans table de correspondance. La valeur est une chaîne, toujours — le front
// traduit (`"true"` → « Fermé »).
public sealed record Facet(
    string Name,
    IReadOnlyList<FacetValue> Values
);

public sealed record FacetValue(
    string Value,
    int Count
)
{
    // La forme wire d'une valeur de facette : celle que le filtre accepte en
    // retour sur la query string — un booléen en minuscules, un nombre en
    // culture invariante.
    public static string Format(object value) => value switch {
        bool flag => flag ? "true" : "false",
        string text => text,
        IFormattable formattable => formattable.ToString(
            format: null,
            formatProvider: CultureInfo.InvariantCulture
        ),
        _ => value.ToString() ?? string.Empty,
    };
}
