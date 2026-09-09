using LoreBank.SharedKernel.Api.Controllers;
using LoreBank.SharedKernel.Api.Signals;
using LoreBank.SharedKernel.Application.Signals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;

namespace LoreBank.SharedKernel.Api.OpenApi;

// La convention qui lit ce que ModuleController affirme par ses types de
// retour : une action qui rend CommandResult sert 204, une qui rend
// CreationResult sert 201 — l'ApiExplorer, lui, inférerait 200 d'un
// ActionResult nu — et une qui rend SignalStreamResult sert 200 en
// text/event-stream sur le schéma Signal (ADR 0026). Elle pose aussi l'identité wire de chaque opération :
// l'operationId `<Controller>_<Action>` (le nom d'action est déjà du contrat —
// CreatedAtAction le cible) et le tag au nom du module, lu au 2ᵉ segment de
// l'assembly du controller comme le sont les codes d'erreur.
//
// Métadonnées seulement, jamais de filtre : [Produces]/[Consumes] sont des
// filtres qui réécriraient le type de média des réponses d'erreur
// (application/problem+json) — le resserrage des media types est l'affaire
// du DescriptionDocumentTransformer.
public sealed class DescriptionConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var produces = ProducesOf(action.ActionMethod.ReturnType);

        if (produces is not null) {
            action.Filters.Add(produces);
        }

        var operationId = $"{action.Controller.ControllerName}_{action.ActionName}";
        var module = ModuleOf(action.Controller.ControllerType.Assembly.GetName().Name!);

        foreach (var selector in action.Selectors) {
            selector.EndpointMetadata.Add(new EndpointNameMetadata(operationId));
            selector.EndpointMetadata.Add(new TagsAttribute(module));
        }
    }

    private static ProducesResponseTypeAttribute? ProducesOf(Type returnType)
    {
        var unwrapped = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;

        if (unwrapped == typeof(CommandResult)) {
            return new ProducesResponseTypeAttribute(StatusCodes.Status204NoContent);
        }

        if (unwrapped == typeof(CreationResult)) {
            return new ProducesResponseTypeAttribute(StatusCodes.Status201Created);
        }

        if (unwrapped == typeof(SignalStreamResult)) {
            return new ProducesResponseTypeAttribute(
                type: typeof(Signal),
                statusCode: StatusCodes.Status200OK,
                contentType: SignalStreamResult.ContentType
            );
        }

        return null;
    }

    // `<Racine>.<Module>.<Couche>` — la règle de nommage que DomainException
    // suppose aussi pour préfixer les codes.
    private static string ModuleOf(string assemblyName)
    {
        var segments = assemblyName.Split('.');

        return segments.Length >= 2
            ? segments[1]
            : assemblyName;
    }
}
