using System.Transactions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Chaque test s'exécute dans un TransactionScope rollbacké, au même niveau
// d'isolation que le TransactionBehavior (ReadCommitted) : un scope Required
// qui rejoint un ambiant d'un niveau différent lève une ArgumentException.
public abstract class BaseIntegrationTest<TFactory> : BaseHostTest<TFactory>
    where TFactory : IntegrationTestWebAppFactory, new()
{
    private IServiceScope? _scope;
    private TransactionScope? _transaction;
    protected ISender Sender = null!;

    protected IServiceProvider ScopeServices => _scope!.ServiceProvider;

    [SetUp]
    public void BaseSetUp()
    {
        // NUnit exécute les [SetUp] de la base d'abord : HostSetUp a déjà
        // touché Factory, donc l'hôte a démarré — et migré — avant l'ouverture
        // du scope ambiant, à laquelle sa migration EF Core ne survivrait pas
        // (HandleAmbientTransactions).
        _transaction = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            transactionOptions: new TransactionOptions {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.Zero,
            },
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
        );

        _scope = Factory.Services.CreateScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
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
