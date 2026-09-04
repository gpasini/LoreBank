using LoreBank.SharedKernel.Api.Problems;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LoreBank.SharedKernel.Api.Validation;

// Remplace le ValidationProblemDetails que produit [ApiController] par défaut :
// ses messages sont du texte anglais qui cite des noms de types .NET
// (« could not be converted to LoreBank.Bank.Application.Commands.DepositMoneyCommand »),
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
            status: Status,
            code: Code,
            parameters: new Dictionary<string, object> {
                ["fields"] = FaultyFields(context.ModelState),
            }
        );

        return ApiProblem.ResultFor(problemDetails);
    }

    private static string[] FaultyFields(ModelStateDictionary modelState) => modelState
        .Where(entry => entry.Value?.Errors.Count > 0)
        .Select(entry => TrimJsonPath(entry.Key))
        .Where(field => field.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string TrimJsonPath(string key) => key.StartsWith(
        value: JsonPathPrefix,
        comparisonType: StringComparison.Ordinal
    )
        ? key[JsonPathPrefix.Length..]
        : key;
}
