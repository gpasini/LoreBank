namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Base des fixtures qui parlent à l'hôte réel SANS transaction rollbackée : les
// contrats HTTP (le TransactionScope ambiant ne traverse pas la frontière HTTP,
// une requête servie par l'hôte écrirait hors du rollback) et les tests qui
// observent un rollback réel (un scope interne non complété condamnerait
// l'ambiant). Fournit l'hôte partagé et le point unique de remise à zéro des
// fakes du module.
public abstract class BaseHostTest<TFactory> where TFactory : IntegrationTestWebAppFactory, new()
{
    protected static TFactory Factory => TestHost<TFactory>.Factory;

    [SetUp]
    public void HostSetUp() => Factory.ResetFakes();

    [TearDown]
    public void HostTearDown() => Factory.ResetFakes();
}
