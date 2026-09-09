using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// La factory redirige TOUTES les chaînes de connexion vers le Testcontainer.
// Sans cette garantie, l'hôte de test d'un module laisserait les DbContext des
// autres modules pointer sur la base réelle du développeur — que le harnais
// migrerait via ModuleMigrator (TestHost). Cet échec-là serait silencieux et
// destructeur : c'est précisément lui qu'on épingle ici.
[TestFixture]
[TestOf(typeof(IntegrationTestWebAppFactory))]
public sealed class ConnectionRedirectTest
{
    private static IEnumerable<IHostModule> Modules => HostModules.All;

    [TestCaseSource(nameof(Modules))]
    public void ConfigureWebHost_ShouldPointEveryDbContextAtTheContainer_WhenTheHostStarts(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        var dbContext = (DbContext) scope.ServiceProvider.GetRequiredService(module.DbContextType);

        dbContext.Database
            .GetConnectionString()
            .Should()
            .Be(TestHost<SharedKernelWebAppFactory>.Factory.ContainerConnectionString);
    }
}
