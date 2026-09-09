using System.Reflection;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Infrastructure.Modules;
using MediatR;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Une query lit par un port de lecture (Readers/), jamais par le repository de
// l'agrégat : un handler de query qui prendrait un repository matérialiserait
// le modèle d'écriture — value objects reconstruits, entité trackée — pour de
// l'affichage, et recréerait la dépendance du Result au modèle d'écriture que
// l'ADR 0012 a coupée. Rien d'autre que cette garde ne le signalerait : le
// code compilerait et les tests du use case resteraient verts.
[TestFixture]
[TestOf(typeof(HostModules))]
public sealed class ApplicationConventionTest
{
    private static IEnumerable<IHostModule> Modules => HostModules.All;

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldKeepQueryHandlersOffAggregateRepositories_WhenTheModuleIsDeclared(IHostModule module)
    {
        // Les ports de repository vivent dans Repositories/ du Domain (ADR
        // 0010) : le segment de namespace suffit à les reconnaître sans
        // imposer d'interface commune.
        var queryHandlerTypes = module.ApplicationAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(HandlesAQuery)
            .ToList();

        queryHandlerTypes.Should().NotBeEmpty();

        foreach (var handlerType in queryHandlerTypes) {
            var repositoryParameters = handlerType
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => (parameter.ParameterType.Namespace ?? string.Empty)
                    .Split('.')
                    .Contains("Repositories")
                )
                .ToList();

            repositoryParameters.Should().BeEmpty($"{handlerType.Name} est un handler de query — il lit par un port de Readers/, pas par le repository de l'agrégat");
        }
    }

    // Une Liste a une seule forme (ADR 0027) : une query qui rend une Page
    // dérive de ListQuery — pas de IQuery<ListPage<>> écrit à la main — et ses
    // filtres sont multi-valeurs, des IReadOnlyList<T> : c'est la forme que
    // les facettes impliquent (OU dans un filtre), et celle que le front
    // apprend une fois. Une propriété scalaire compilerait et se lierait sans
    // que rien d'autre ne le signale.
    [TestCaseSource(nameof(Modules))]
    public void All_ShouldShapeEveryListQueryOnTheSocle_WhenTheModuleIsDeclared(IHostModule module)
    {
        var pageQueries = module.ApplicationAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(RendersAPage)
            .ToList();

        foreach (var queryType in pageQueries) {
            DerivesFromListQuery(queryType).Should().BeTrue(
                $"{queryType.Name} rend une Page : elle dérive de ListQuery<TItem>, la forme unique d'une Liste"
            );

            var scalarFilters = queryType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => !IsMultiValued(property.PropertyType))
                .Select(property => property.Name)
                .ToList();

            scalarFilters.Should().BeEmpty(
                $"les filtres de {queryType.Name} sont multi-valeurs (IReadOnlyList<T>) — OU dans un filtre, ET entre filtres"
            );
        }
    }

    private static bool RendersAPage(Type type) => type
        .GetInterfaces()
        .Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IQuery<>)
            && contract.GetGenericArguments()[0] is { IsGenericType: true } response
            && response.GetGenericTypeDefinition() == typeof(ListPage<>)
        );

    private static bool DerivesFromListQuery(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ListQuery<>)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsMultiValued(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    private static bool HandlesAQuery(Type type) => type
        .GetInterfaces()
        .Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
            && contract
                .GetGenericArguments()[0]
                .GetInterfaces()
                .Any(request => request.IsGenericType && request.GetGenericTypeDefinition() == typeof(IQuery<>))
        );
}
