using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Démarre l'hôte réel contre un PostgreSQL Testcontainers. Chaque module dérive
// cette factory pour enregistrer ses fakes (ConfigureModuleContainer) et les
// remettre à zéro entre deux tests (ResetFakes).
public abstract class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("lorebank_tests")
        .WithUsername("lorebank")
        .WithPassword("lorebank")
        .Build();

    public string ContainerConnectionString => _dbContainer.GetConnectionString();

    // Redirige TOUTES les chaînes de connexion vers le conteneur : chaque
    // ConfigureDbContext de module lit la sienne via IConfiguration, et un
    // schéma PostgreSQL par module rend le partage d'une même base sans
    // conflit. Ne rediriger que le DbContext du module courant laisserait ceux
    // des autres modules pointer sur la base réelle du développeur — que le
    // harnais migrerait via ModuleMigrator (TestHost)
    // (ConnectionRedirectTest épingle cette garantie).
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // L'environnement pilote le chargement de la configuration
        // (appsettings.Development.json) : on l'épingle plutôt que d'hériter
        // de l'ASPNETCORE_ENVIRONMENT du shell qui lance les tests, pour que
        // la config de l'hôte de test ne varie pas d'un poste à l'autre
        // (TestHostEnvironmentTest tient cette garantie).
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configuration) => {
                var redirected = context.Configuration
                    .GetSection("ConnectionStrings")
                    .GetChildren()
                    .ToDictionary(
                        keySelector: child => $"ConnectionStrings:{child.Key}",
                        elementSelector: _ => (string?)ContainerConnectionString
                    );

                configuration.AddInMemoryCollection(redirected);
            }
        );
    }

    // La substitution d'un service enregistré par un Module Autofac ne peut pas
    // se faire dans ConfigureTestServices : celui-ci peuple l'IServiceCollection
    // avant que le ConfigureContainer de l'hôte ne le transpose dans le
    // conteneur Autofac et n'exécute les Module des Infrastructures par-dessus.
    // La dernière inscription Autofac gagne — on ajoute donc un second
    // ConfigureContainer après celui de l'hôte, pour que les fakes gagnent à
    // leur tour.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureContainer<ContainerBuilder>(ConfigureModuleContainer);

        return base.CreateHost(builder);
    }

    protected virtual void ConfigureModuleContainer(ContainerBuilder builder)
    {
    }

    // Point unique de remise à zéro des fakes du module, appelé par BaseHostTest
    // au SetUp et au TearDown de chaque test.
    public virtual void ResetFakes()
    {
    }

    public async Task StartAsync() => await _dbContainer.StartAsync();

    public async Task StopAsync() => await _dbContainer.StopAsync();
}
