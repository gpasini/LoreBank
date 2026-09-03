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
