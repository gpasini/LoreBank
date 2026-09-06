using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// La résolution « nom de module → DbContext », écrite une fois : la
// correspondance est insensible à la casse — le premier segment d'un
// discriminant est en minuscules (« bank ») quand ModuleName est en Pascal
// (« Bank »), et l'unicité des schémas (ModuleCompositionTest) garantit que
// deux modules ne diffèrent jamais par la seule casse. L'échec nomme le
// module absent et pointe HostModules.All — un message unique, enrichi du
// contexte de chaque appelant. ModuleDbContextsTest épingle la politique.
internal static class ModuleDbContexts
{
    internal static ModuleDbContext Resolve(
        IEnumerable<IHostModule> modules,
        IServiceProvider services,
        string moduleName,
        string purpose
    )
    {
        var module = modules.SingleOrDefault(candidate => candidate.ModuleName.Equals(
            value: moduleName,
            comparisonType: StringComparison.OrdinalIgnoreCase
        ));

        if (module is null) {
            throw new InvalidOperationException(
                $"Le module « {moduleName} » n'est monté par aucun IHostModule : {purpose}. "
                + "Les modules montés sont ceux de HostModules.All — et ceux qu'un harnais ajoute."
            );
        }

        return Resolve(
            services: services,
            module: module
        );
    }

    internal static ModuleDbContext Resolve(
        IServiceProvider services,
        IHostModule module
    ) => (ModuleDbContext)services.GetRequiredService(module.DbContextType);
}
