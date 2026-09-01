namespace LoreBank.Bank.Test.Infrastructure.Setups;

// L'hôte et son conteneur PostgreSQL sont partagés par toute la suite. Extrait de
// BaseIntegrationTest parce que les tests de rollback en ont besoin sans hériter
// de son TransactionScope.
public static class TestHost
{
    static TestHost()
    {
        Factory = new IntegrationTestWebAppFactory();
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

    public static IntegrationTestWebAppFactory Factory { get; }
}
