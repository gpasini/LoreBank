using System.Reflection;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La découverte des handlers d'integration events d'un module, même geste que
// le scan des IDomainEventHandler<> côté hôte : une ligne à recopier par
// module serait oubliable, et l'oubli silencieux — un event publié sans
// handler enregistré est simplement marqué livré. Domain et Application sont
// scannées toutes deux : où les handlers vivent est une décision du module,
// pas du socle.
public static class IntegrationEventHandlers
{
    public static IReadOnlyList<IntegrationEventHandlerRegistration> DiscoverIn(IHostModule module) =>
        DiscoverIn(
            moduleName: module.ModuleName,
            assemblies: [module.DomainAssembly, module.ApplicationAssembly]
        );

    internal static IReadOnlyList<IntegrationEventHandlerRegistration> DiscoverIn(
        string moduleName,
        IReadOnlyList<Assembly> assemblies
    ) =>
        assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(candidate => candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .Select(candidate => new IntegrationEventHandlerRegistration(
                    handlerType: type,
                    eventType: candidate.GetGenericArguments().Single(),
                    moduleName: moduleName
                ))
            )
            .ToList();
}
