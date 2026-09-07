using System.Globalization;
using System.Text.RegularExpressions;

namespace LoreBank.SharedKernel.Domain.Exceptions;

public abstract partial class DomainException : Exception
{
    private const string ExceptionSuffix = "Exception";

    private const string SharedKernelSegment = "SharedKernel";

    protected DomainException(Dictionary<string, object> parameters)
    {
        Parameters = parameters;
        Code = CodeOf(GetType());
    }

    protected DomainException() : this([])
    {
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object> Parameters { get; }

    // Destiné aux logs et aux stack traces uniquement : le client reçoit le code et les paramètres.
    public override string Message => Parameters.Count == 0
        ? Code
        : $"{Code} ({string.Join(
            separator: ", ",
            values: Parameters.Select(Format)
        )})";

    // La dérivation par type, exposée pour qui décrit les codes sans lever
    // d'exception : le scan qui énumère les codes de la Description OpenAPI ne
    // peut pas instancier des exceptions à paramètres.
    public static string CodeOf(Type type)
    {
        var violation = ToScreamingSnakeCase(TrimExceptionSuffix(type.Name));
        var module = DeriveModule(type.Namespace);

        return module is null
            ? violation
            : $"{module}.{violation}";
    }

    // Le 2e segment du namespace nomme le module ; le SharedKernel n'en est pas un et ne préfixe rien.
    private static string? DeriveModule(string? @namespace)
    {
        var segments = @namespace?.Split('.') ?? [];

        return segments.Length < 2 || segments[1] == SharedKernelSegment
            ? null
            : ToScreamingSnakeCase(segments[1]);
    }

    private static string TrimExceptionSuffix(string name) => name.EndsWith(
        value: ExceptionSuffix,
        comparisonType: StringComparison.Ordinal
    )
        ? name[..^ExceptionSuffix.Length]
        : name;

    private static string ToScreamingSnakeCase(string value) => WordBoundary()
        .Replace(
            input: value,
            replacement: "_"
        )
        .ToUpperInvariant();

    private static string Format(KeyValuePair<string, object> parameter) =>
        $"{parameter.Key}={Convert.ToString(
            value: parameter.Value,
            provider: CultureInfo.InvariantCulture
        )}";

    // Alternance : transition minuscule/chiffre → majuscule (camelCase classique),
    // ou fin d'un acronyme suivi d'un mot capitalisé (ex. IBANNotFound → IBAN_NOT_FOUND).
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();
}
