using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Api.OpenApi;

// Le geste unique par lequel l'hôte monte la Description OpenAPI : la
// convention qui lit les types de retour de ModuleController, les
// transformers qui complètent le document, et le scan des codes d'erreur sur
// les assemblies Domain et Application des modules montés. L'Api ne connaît
// pas IHostModule — l'hôte lui passe les assemblies. Inconditionnel : le service n'a aucune
// surface réseau (l'endpoint /openapi, lui, reste à la discrétion de l'hôte),
// et l'émission au build en a besoin quel que soit l'environnement.
public static class OpenApiDescriptionServiceCollectionExtensions
{
    public static IServiceCollection AddOpenApiDescription(
        this IServiceCollection services,
        string title,
        IEnumerable<Assembly> assemblies
    )
    {
        var errorCodes = ErrorCodes.DiscoverIn(assemblies);

        services.Configure<MvcOptions>(options => options.Conventions.Add(new DescriptionConvention()));

        services.AddOpenApi(options => {
                options.AddSchemaTransformer(new DescriptionSchemaTransformer());
                options.AddDocumentTransformer(new DescriptionDocumentTransformer(
                        title: title,
                        errorCodes: errorCodes
                    )
                );
            }
        );

        return services;
    }
}
