using System.Reflection;
using Autofac.Core;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// La base des adapters IHostModule : l'identité du module — ses trois
// assemblies et son nom — est dérivée du DbContext ancré en paramètre
// générique, au lieu d'être saisie membre par membre. Trois typeof
// indépendants laissaient une DomainAssembly mal pointée rendre le dispatch
// des events silencieusement vide (ADR 0007) ; dérivée dans le constructeur,
// l'identité d'un module mal nommé casse au premier contact avec
// HostModules.All, avant que quoi que ce soit ne soit monté.
public abstract class HostModule<TDbContext> : IHostModule
    where TDbContext : ModuleDbContext
{
    protected HostModule()
    {
        var (root, module) = ModuleAssemblyName.Parse(
            typeof(TDbContext).Assembly.GetName().Name ?? string.Empty
        );

        ModuleName = module;
        ControllerAssembly = Assembly.Load($"{root}.{module}.Api");
        ApplicationAssembly = Assembly.Load($"{root}.{module}.Application");
        DomainAssembly = Assembly.Load($"{root}.{module}.Domain");
    }

    public string ModuleName { get; }

    public Assembly ControllerAssembly { get; }

    public Assembly ApplicationAssembly { get; }

    public Assembly DomainAssembly { get; }

    // La contrainte générique rend « un DbContext qui ne dérive pas de
    // ModuleDbContext » incompilable — l'assertion de composition qui le
    // vérifiait a disparu avec elle.
    public Type DbContextType => typeof(TDbContext);

    public abstract IModule AutofacModule { get; }

    // La persistance par défaut est elle aussi dérivée de l'identité : la clé
    // « <Module>Db » sous ConnectionStrings, validée et montée par
    // AddModuleDbContext (ADR 0008). Une autre clé = surcharge d'une ligne qui
    // rappelle l'extension ; un provider exotique = surcharge complète,
    // invariants à la charge du module.
    public virtual void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    ) => services.AddModuleDbContext<TDbContext>(
        configuration: configuration,
        connectionStringName: $"{ModuleName}Db"
    );
}
