using System.Reflection;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// Tout ce que l'hôte doit savoir d'un module pour le monter. Les assemblies
// sont des données et non des hooks : MediatR et AddApplicationPart ne
// s'inscrivent qu'en un seul appel côté hôte, qui doit donc collecter les
// assemblies de tous les modules avant d'inscrire quoi que ce soit.
public interface IHostModule
{
    Assembly ControllerAssembly { get; }

    Assembly ApplicationAssembly { get; }

    IModule AutofacModule { get; }

    void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    );

    // Le mécanisme de migration appartient au module ; la politique — ne migrer
    // qu'en Development — reste dans l'hôte, qui décide quand appeler.
    Task MigrateAsync(IServiceProvider services);
}
