using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// L'hôte et son conteneur PostgreSQL sont partagés par toutes les fixtures d'un
// même assembly de test (chaque assembly tourne dans son propre process, donc
// chaque TFactory a le sien). Statique plutôt que porté par une fixture : les
// tests de rollback en ont besoin sans hériter du TransactionScope de
// BaseIntegrationTest.
public static class TestHost<TFactory> where TFactory : IntegrationTestWebAppFactory, new()
{
    static TestHost()
    {
        Factory = new TFactory();
        Factory.StartAsync().GetAwaiter().GetResult();
        _ = Factory.Services;

        // L'API ne migre plus à son démarrage (ADR 0006) : c'est le harnais
        // qui prépare le schéma du Testcontainer, par la même routine que le
        // verbe `migrate` de l'hôte.
        ModuleMigrator.MigrateAsync(
                services: Factory.Services,
                modules: HostModules.All
            )
            .GetAwaiter()
            .GetResult();

        AppDomain.CurrentDomain.ProcessExit += (
            _,
            _
        ) =>
        {
            Factory.StopAsync().GetAwaiter().GetResult();
            Factory.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
    }

    public static TFactory Factory { get; }
}
