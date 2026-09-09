using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

// La ressource qu'un Signal concerne (ADR 0026) : un genre stable choisi en
// kebab-case — la grille du discriminant d'outbox, jamais un nom de type
// .NET — et l'identifiant de l'instance. Deux frontières la construisent :
// le publisher, depuis l'event qui la nomme (Of), et le filtre du flux,
// depuis le paramètre « kind/guid » du client (Parse). Les deux valident ;
// un genre mal formé est une erreur du socle, comme un IBAN invalide.
public sealed partial class SignalResource : ValueObject
{
    private const char Separator = '/';

    private SignalResource(
        string kind,
        Guid id
    )
    {
        Kind = kind;
        Id = id;
    }

    public string Kind { get; }

    public Guid Id { get; }

    public static SignalResource Of(
        string kind,
        Guid id
    )
    {
        if (!KindFormat().IsMatch(kind)) {
            throw new InvalidSignalResourceException($"{kind}{Separator}{id}");
        }

        return new SignalResource(
            kind: kind,
            id: id
        );
    }

    public static SignalResource Parse(string value)
    {
        var separator = value.IndexOf(Separator);

        if (separator < 0 || !Guid.TryParse(
                input: value[(separator + 1)..],
                result: out var id
            )) {
            throw new InvalidSignalResourceException(value);
        }

        var kind = value[..separator];

        if (!KindFormat().IsMatch(kind)) {
            throw new InvalidSignalResourceException(value);
        }

        return new SignalResource(
            kind: kind,
            id: id
        );
    }

    protected override IEnumerable<object?> GetEqualityComponents() => [Kind, Id];

    public override string ToString() => $"{Kind}{Separator}{Id}";

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex KindFormat();
}
