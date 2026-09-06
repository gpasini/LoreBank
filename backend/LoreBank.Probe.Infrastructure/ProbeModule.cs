using System.Reflection;
using Autofac.Core;
using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Probe.Infrastructure;

// Le module-terrain du harnais du socle (ADR 0017) : absent de
// HostModules.All — jamais monté par l'hôte réel, ce sont les factories de
// test qui l'enregistrent (AdditionalModules) et TestHost qui le migre.
// Adapter IHostModule écrit à la main plutôt que dérivé de
// HostModule<TDbContext> : la dérivation complète de l'identité — quatre
// assemblies chargées eagerly — est une convention des modules métier, pas
// une loi du seam. Le terrain n'a ni Api, ni Application, ni Domain : ses
// trois assemblies pointent sur lui-même, et les scans n'y trouvent rien.
public sealed class ProbeModule : IHostModule
{
    public string ModuleName => "Probe";

    public Assembly ControllerAssembly => typeof(ProbeModule).Assembly;

    public Assembly ApplicationAssembly => typeof(ProbeModule).Assembly;

    public Assembly DomainAssembly => typeof(ProbeModule).Assembly;

    // Aucun service à enregistrer : le terrain n'a que son DbContext.
    public IModule AutofacModule => new EmptyModule();

    public Type DbContextType => typeof(ProbeDbContext);

    public void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    ) => services.AddModuleDbContext<ProbeDbContext>(
        configuration: configuration,
        connectionStringName: "ProbeDb"
    );

    private sealed class EmptyModule : Autofac.Module;
}
