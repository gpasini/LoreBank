using LoreBank.SharedKernel.Application;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LoreBank.SharedKernel.Api.OpenApi;

// Deux corrections de forme sur les schémas générés :
// - un `decimal` est décrit `number | string` avec un pattern par le
//   générateur (System.Text.Json accepte les deux en entrée) ; le serveur
//   n'émet jamais qu'un nombre, et c'est un nombre que le Client doit
//   envoyer — la Description dit `number` ;
// - une propriété [RouteBound] d'une commande n'appartient pas au body : la
//   route l'écrase (ADR 0012). Elle est retirée du schéma, et de ses champs
//   requis.
public sealed class DescriptionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (IsDecimal(context.JsonTypeInfo.Type)) {
            schema.Type = JsonSchemaType.Number;
            schema.Pattern = null;
            schema.Format = null;
        }

        if (context.JsonPropertyInfo is null) {
            foreach (var property in context.JsonTypeInfo.Properties.Where(IsRouteBound)) {
                schema.Properties?.Remove(property.Name);
                schema.Required?.Remove(property.Name);
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsDecimal(Type type) => type == typeof(decimal) || type == typeof(decimal?);

    private static bool IsRouteBound(System.Text.Json.Serialization.Metadata.JsonPropertyInfo property) =>
        property.AttributeProvider?.IsDefined(
            attributeType: typeof(RouteBoundAttribute),
            inherit: true
        ) == true;
}
