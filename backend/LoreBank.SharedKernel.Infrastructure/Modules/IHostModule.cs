using System.Reflection;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// Tout ce que l'hôte doit savoir d'un module pour le monter. Les assemblies
// sont des données et non des hooks : MediatR et AddApplicationPart ne
// s'inscrivent qu'en un seul appel côté hôte, qui doit donc collecter les
// assemblies de tous les modules avant d'inscrire quoi que ce soit.
// La façon prévue d'implémenter ce seam est la base HostModule<TDbContext>,
// qui dérive le nom et les assemblies du DbContext (ADR 0007).
public interface IHostModule
{
    // Le nom du module (« Bank ») : le même segment que DomainException lit
    // dans le namespace pour préfixer ses codes — ModuleCompositionTest
    // épingle la correspondance.
    string ModuleName { get; }

    Assembly ControllerAssembly { get; }

    Assembly ApplicationAssembly { get; }

    // L'assembly Domain du module : l'hôte y scanne les IDomainEventHandler<>,
    // pour que l'enregistrement des handlers ne soit pas une ligne à recopier
    // par module — la recopie était oubliable, et l'oubli silencieux.
    Assembly DomainAssembly { get; }

    IModule AutofacModule { get; }

    // Le type au singulier encode la doctrine « un DbContext par module » : ni
    // liste vide ni second contexte possible. ModuleMigrator s'en sert pour
    // migrer, et le harnais de test pour vérifier la redirection des
    // connexions — sans ce fait sur le seam, il le retrouvait par scan
    // d'assembly (voir ADR 0005).
    Type DbContextType { get; }

    // La configuration de persistance, elle, reste au module : nom de chaîne
    // de connexion, options du provider.
    void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    );
}
