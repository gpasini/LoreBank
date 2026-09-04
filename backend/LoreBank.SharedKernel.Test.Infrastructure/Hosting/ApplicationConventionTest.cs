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
