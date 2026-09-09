using Autofac;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    // L'horloge du harnais (ADR 0024) : un test y pose l'Instant que toute
    // Application demandera, ResetFakes l'efface.
    public ConfigurableTimeProvider TimeProvider => Services.GetRequiredService<ConfigurableTimeProvider>();

    // Les modules que cette factory monte en plus de HostModules.All — le
    // ProbeModule du harnais du socle (ADR 0017), jamais un module métier.
    // La base fait pour eux ce que Program.cs fait pour la liste de l'hôte :
    // chaîne de connexion vers le conteneur, DbContext par leur
    // ConfigureDbContext, instance sur le seam au conteneur — et TestHost
    // les ajoute à sa passe de migration.
    public virtual IReadOnlyCollection<IHostModule> AdditionalModules => [];

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

                // Les modules additionnels n'ont pas de clé dans appsettings :
                // leur chaîne naît ici, sur la clé par défaut de leur
                // ConfigureDbContext, avec la même destination que la
                // redirection ci-dessus.
                configuration.AddInMemoryCollection(AdditionalModules.ToDictionary(
                        keySelector: module => $"ConnectionStrings:{module.ModuleName}Db",
                        elementSelector: _ => (string?)ContainerConnectionString
                    )
                );

                // La cadence de fond est neutralisée dans tous les hôtes de
                // test : les tests d'outbox pilotent OutboxProcessor
                // eux-mêmes, une passe concurrente du hosted service rendrait
                // leurs compteurs flaky.
                configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                        ["IntegrationEvents:PollingSeconds"] = "3600",
                    }
                );
            }
        );

        // L'hôte enregistre TimeProvider.System via builder.Services, pas via
        // un Module Autofac : ConfigureTestServices suffit à le remplacer par
        // le fake — la dernière inscription gagne.
        builder.ConfigureTestServices(services => {
                services.AddSingleton<ConfigurableTimeProvider>();
                services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<ConfigurableTimeProvider>());
            }
        );

        // Le geste que Program.cs fait pour chaque module de HostModules.All,
        // fait ici pour les modules additionnels : leur DbContext se monte par
        // le même membre du seam.
        builder.ConfigureServices((
                context,
                services
            ) =>
            {
                foreach (var module in AdditionalModules) {
                    module.ConfigureDbContext(
                        services: services,
                        configuration: context.Configuration
                    );
                }
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
        // Les modules additionnels rejoignent le seam au conteneur :
        // OutboxPublisher et OutboxProcessor consomment
        // l'IEnumerable<IHostModule> résolu, pas HostModules.All.
        builder.ConfigureContainer<ContainerBuilder>(container => {
                foreach (var module in AdditionalModules) {
                    container.RegisterInstance(module).As<IHostModule>();
                }
            }
        );

        builder.ConfigureContainer<ContainerBuilder>(ConfigureModuleContainer);

        return base.CreateHost(builder);
    }

    protected virtual void ConfigureModuleContainer(ContainerBuilder builder)
    {
    }

    // Point unique de remise à zéro des fakes, appelé par BaseHostTest au SetUp
    // et au TearDown de chaque test. La base efface les fakes du socle ; une
    // factory de module qui surcharge appelle la base, puis remet les siens.
    public virtual void ResetFakes() => TimeProvider.Reset();

    public async Task StartAsync() => await _dbContainer.StartAsync();

    public async Task StopAsync() => await _dbContainer.StopAsync();
}
