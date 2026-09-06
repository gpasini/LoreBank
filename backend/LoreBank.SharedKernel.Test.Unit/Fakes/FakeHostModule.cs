using System.Reflection;
using Autofac.Core;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Un IHostModule réduit à son identité : nom et type de DbContext — ce que la
// résolution nom → DbContext consomme. Le reste du seam n'est pas simulé.
public sealed class FakeHostModule(
    string moduleName,
    Type dbContextType
) : IHostModule
{
    public string ModuleName => moduleName;

    public Assembly ControllerAssembly => typeof(FakeHostModule).Assembly;

    public Assembly ApplicationAssembly => typeof(FakeHostModule).Assembly;

    public Assembly DomainAssembly => typeof(FakeHostModule).Assembly;

    public IModule AutofacModule => throw new NotSupportedException("le fake ne se monte pas dans un conteneur");

    public Type DbContextType => dbContextType;

    public void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
    }
}
