using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

public sealed partial class Iban : SimpleValueObject<string>
{
    public Iban(string value) : base(Normalize(value))
    {
        if (!Format().IsMatch(Value)) {
            throw new InvalidIbanException(value);
        }
    }

    private static string Normalize(string value) => value
        .Replace(
            oldValue: " ",
            newValue: string.Empty
        )
        .ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex Format();
}
