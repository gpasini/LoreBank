using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public abstract class BankIntegrationTest : BaseIntegrationTest<BankWebAppFactory>
{
    protected DbSetup DbSetup = null!;

    // Les [SetUp] des bases ont déjà tourné : le scope DI du test existe.
    [SetUp]
    public void BankSetUp() => DbSetup = new DbSetup(ScopeServices);
}
