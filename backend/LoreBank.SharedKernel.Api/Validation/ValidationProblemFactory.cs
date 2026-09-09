using System.Text.Json;
using LoreBank.SharedKernel.Api.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LoreBank.SharedKernel.Api.Validation;

// Remplace le ValidationProblemDetails que produit [ApiController] par défaut :
// ses messages sont du texte anglais qui cite des noms de types .NET
// (« could not be converted to
// LoreBank.Bank.Application.Commands.DepositMoney.DepositMoneyCommand »),
// donc inaffichable et fuyant. On n'en garde que ce qui est un contrat : les
// champs fautifs, sous un code que le front traduit comme les autres.
public static class ValidationProblemFactory
{
    public const string Code = "VALIDATION_FAILED";

    private const int Status = StatusCodes.Status400BadRequest;

    // System.Text.Json préfixe ses clés du chemin JSON de la valeur fautive.
    private const string JsonPathPrefix = "$.";

    public static IActionResult Create(ActionContext context)
    {
        var problemDetails = ApiProblem.Create(
            httpContext: context.HttpContext,
            status: Status,
            code: Code,
            parameters: new Dictionary<string, object> {
                ["fields"] = FaultyFields(context.ModelState),
            }
        );

        return ApiProblem.ResultFor(problemDetails);
    }

    // Un champ est nommé comme sur le fil : le chemin JSON d'un body est déjà
    // en camelCase, la clé d'une propriété liée sur la query string (une
    // ListQuery, ADR 0027) porte le nom .NET — elle est ramenée à la casse de
    // la Description, segment par segment.
    private static string[] FaultyFields(ModelStateDictionary modelState) => modelState
        .Where(entry => entry.Value?.Errors.Count > 0)
        .Select(entry => ToCamelCase(TrimJsonPath(entry.Key)))
        .Where(field => field.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string ToCamelCase(string field) => string.Join(
        separator: '.',
        values: field.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName)
    );

    private static string TrimJsonPath(string key) => key.StartsWith(
        value: JsonPathPrefix,
        comparisonType: StringComparison.Ordinal
    )
        ? key[JsonPathPrefix.Length..]
        : key;
}
