using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoreBank.SharedKernel.Test.Unit.Modules;

// La Disponibilité se prouve ici, sans hôte : un module dont la base répond
// (Sqlite en mémoire) à côté d'un module dont la base est injoignable (un
// port fermé, Npgsql) — le conteneur partagé du harnais ne se coupe pas
// sans casser les fixtures voisines (ADR 0022).
[TestFixture]
[TestOf(typeof(ModuleDatabasesHealthCheck))]
public sealed class ModuleDatabasesHealthCheckTest
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    [TearDown]
    public async Task TearDown() => await _connection.DisposeAsync();

    [Test]
    public async Task CheckHealthAsync_ShouldBeHealthy_WhenEveryModuleDatabaseAnswers()
    {
        // Arrange

        await using var services = Services(
            reachable: true,
            unreachable: false
        );

        // Act

        var result = await Check(services).CheckHealthAsync(new HealthCheckContext());

        // Assert

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().BeEquivalentTo(new Dictionary<string, object> {
            ["Reachable"] = "Healthy",
        });
    }

    [Test]
    public async Task CheckHealthAsync_ShouldBeUnhealthyAndNameTheModule_WhenOneDatabaseIsUnreachable()
    {
        // Arrange

        await using var services = Services(
            reachable: true,
            unreachable: true
        );

        // Act

        var result = await Check(services).CheckHealthAsync(new HealthCheckContext());

        // Assert — le module joignable reste Healthy dans la même réponse :
        // c'est lui qui dit à l'exploitant où regarder.

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().BeEquivalentTo(new Dictionary<string, object> {
            ["Reachable"] = "Healthy",
            ["Unreachable"] = "Unhealthy",
        });
    }

    private static ModuleDatabasesHealthCheck Check(ServiceProvider services) => new(
        modules: services.GetRequiredService<IEnumerable<IHostModule>>(),
        services: services
    );

    private ServiceProvider Services(
        bool reachable,
        bool unreachable
    )
    {
        var services = new ServiceCollection();

        services.AddSingleton<Domain.Events.IDomainEventDispatcher, RecordingDomainEventDispatcher>();

        if (reachable) {
            services.AddDbContext<BareModuleDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<IHostModule>(new FakeHostModule(
                moduleName: "Reachable",
                dbContextType: typeof(BareModuleDbContext)
            ));
        }

        if (unreachable) {
            // Un port fermé sur la boucle locale : refus immédiat, aucune
            // attente — et un Timeout court au cas où quelque chose écoute.
            services.AddDbContext<TestModuleDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=nope;Username=nope;Password=nope;Timeout=1"
            ));
            services.AddSingleton<IHostModule>(new FakeHostModule(
                moduleName: "Unreachable",
                dbContextType: typeof(TestModuleDbContext)
            ));
        }

        return services.BuildServiceProvider();
    }
}
