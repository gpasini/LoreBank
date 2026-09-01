using System.Transactions;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public abstract class BaseIntegrationTest
{
    private IServiceScope? _scope;
    private TransactionScope? _transaction;
    protected ISender Sender = null!;
    protected DbSetup DbSetup = null!;

    // Force l'initialisation de TestHost avant que BaseSetUp n'ouvre son
    // TransactionScope : sans ça, l'hôte démarrerait sous une transaction
    // ambiante et sa migration EF Core échouerait (HandleAmbientTransactions).
    static BaseIntegrationTest()
    {
        _ = TestHost.Factory;
    }

    [SetUp]
    public void BaseSetUp()
    {
        _transaction = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            transactionOptions: new TransactionOptions {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.Zero,
            },
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
        );

        _scope = TestHost.Factory.Services.CreateScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbSetup = new DbSetup(_scope.ServiceProvider);

        TestHost.Factory.Services.GetRequiredService<ConfigurableWelcomeLetterSender>().Reset();
    }

    [TearDown]
    public void BaseTearDown()
    {
        _scope?.Dispose();
        _scope = null;
        _transaction?.Dispose();
        _transaction = null;
    }

    protected T GetService<T>() where T : notnull => _scope!.ServiceProvider.GetRequiredService<T>();
}
