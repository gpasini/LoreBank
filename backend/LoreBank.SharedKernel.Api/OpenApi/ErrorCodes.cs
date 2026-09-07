using System.Reflection;
using LoreBank.SharedKernel.Api.Validation;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Api.OpenApi;

// L'énumération des codes d'erreur que l'API peut servir, dérivée des types :
// chaque DomainException concrète des assemblies données (Domain *et*
// Application de chaque module monté — les NotFoundException d'un module
// vivent dans son Application, à côté des handlers qui les lèvent), celles du
// SharedKernel — toujours, elles n'appartiennent à aucun module — et le code
// du 400 de binding. C'est ce que la Description
// publie sous le schéma ErrorCode : le Client obtient une union fermée, et une
// table de traduction typée sur elle est complète ou ne compile pas.
public static class ErrorCodes
{
    public const string SchemaName = "ErrorCode";

    public static IReadOnlyList<string> DiscoverIn(IEnumerable<Assembly> assemblies) => assemblies
        .Append(typeof(DomainException).Assembly)
        .Distinct()
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(DomainException).IsAssignableFrom(type))
        .Select(DomainException.CodeOf)
        .Append(ValidationProblemFactory.Code)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
