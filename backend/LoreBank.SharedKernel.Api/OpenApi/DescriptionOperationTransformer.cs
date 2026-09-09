using LoreBank.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LoreBank.SharedKernel.Api.OpenApi;

// Une Liste sous une ressource (ADR 0027) : la ListQuery liée [FromQuery]
// porte une propriété [RouteBound] que le controller réécrit depuis la route
// (`query with { AccountId = id }`), comme une commande sur route mixte
// (ADR 0012). L'ApiExplorer, lui, la décrit en paramètre de query string —
// le Client l'enverrait en double, la route gagnant en silence. Elle est
// retirée ici, où la description MVC de l'opération dit encore sur quelle
// propriété chaque paramètre se lie ; le pendant body est l'affaire du
// DescriptionSchemaTransformer.
public sealed class DescriptionOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var routeBound = context.Description.ParameterDescriptions
            .Where(parameter => parameter.Source == BindingSource.Query && IsRouteBound(parameter.ModelMetadata))
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (routeBound.Count > 0 && operation.Parameters is not null) {
            operation.Parameters = operation.Parameters
                .Where(parameter => !(parameter.In == ParameterLocation.Query && routeBound.Contains(parameter.Name ?? string.Empty)))
                .ToList();
        }

        return Task.CompletedTask;
    }

    private static bool IsRouteBound(ModelMetadata? metadata) =>
        metadata is { ContainerType: not null, PropertyName: not null }
        && metadata.ContainerType.GetProperty(metadata.PropertyName)?.IsDefined(
            attributeType: typeof(RouteBoundAttribute),
            inherit: true
        ) == true;
}
