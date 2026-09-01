using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Bic : SimpleValueObject<string>
{
    public Bic(string value) : base(Normalize(value))
    {
        if (!Format().IsMatch(Value)) {
            throw new InvalidBicException(value);
        }
    }

    private static string Normalize(string value) => value
        .Replace(
            oldValue: " ",
            newValue: string.Empty
        )
        .ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex Format();
}
