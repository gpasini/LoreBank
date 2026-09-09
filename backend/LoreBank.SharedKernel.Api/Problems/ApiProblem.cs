using LoreBank.SharedKernel.Api.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace LoreBank.SharedKernel.Api.Problems;

// Toutes les erreurs de l'API ont la même forme : le libellé standard du statut
// pour `title`, jamais de `detail`, la Corrélation en `traceId` (ADR 0022 :
// un 500 sans identifiant n'est pas diagnosticable), et le couple
// `code`/`parameters` en extensions quand l'erreur en porte un. Centralisé ici
// pour que les trois portes de sortie — filtre métier, échec de binding,
// exception non gérée — ne divergent pas au fil des ajouts.
public static class ApiProblem
{
    // Le type de média de la RFC 9457. Les trois portes de sortie le posent
    // explicitement : sans ça, la négociation de contenu de MVC retomberait sur
    // `application/json` et le front ne pourrait pas distinguer une erreur d'une
    // réponse nominale sur le seul en-tête.
    public const string ContentType = "application/problem+json";

    // MVC ajoute le charset de lui-même à ses réponses ; celui qui écrit
    // directement dans le corps doit le poser à la main pour que l'en-tête soit
    // exactement le même des deux côtés.
    public const string ContentTypeWithCharset = $"{ContentType}; charset=utf-8";

    public static ProblemDetails Create(
        HttpContext httpContext,
        int status
    ) => new() {
        Title = ReasonPhrases.GetReasonPhrase(status),
        Status = status,
        Extensions = {
            ["traceId"] = Correlation.Correlation.Of(httpContext),
        },
    };

    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string code,
        IReadOnlyDictionary<string, object> parameters
    )
    {
        var problemDetails = Create(
            httpContext: httpContext,
            status: status
        );

        problemDetails.Extensions["code"] = code;
        problemDetails.Extensions["parameters"] = parameters;

        return problemDetails;
    }

    public static ObjectResult ResultFor(ProblemDetails problemDetails) => new(problemDetails) {
        StatusCode = problemDetails.Status,
        ContentTypes = { ContentType },
    };
}
