using LoreBank.Ledger.Application.IntegrationEvents;
using LoreBank.Ledger.Test.Infrastructure.Builders;

namespace LoreBank.Ledger.Test.Infrastructure.Setups;

// Les écritures du Ledger naissent des integration events : un geste par
// event consommé (ADR 0030), qui joue le handler lui-même — le vrai use case
// d'écriture du module — sous le TransactionScope rollbacké de la fixture,
// sans passer par l'outbox (le chemin complet est prouvé par CqsContractTest).
// Le compte est comblé depuis le dernier créé, ou ouvert s'il n'y en a aucun.
public partial class DbSetup
{
    public DbSetup RecordDeposit(Action<MoneyDepositedIntegrationEventBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(RecordDeposit),
            step: async () =>
            {
                var builder = new MoneyDepositedIntegrationEventBuilder();

                configure?.Invoke(builder);

                if (builder.AccountId is null) {
                    builder.Of(await LastOrNewAccountIdAsync());
                }

                await GetService<MoneyDepositedIntegrationEventHandler>().HandleAsync(
                    integrationEvent: builder.Build(),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return this;
    }

    public DbSetup RecordWithdrawal(Action<MoneyWithdrawnIntegrationEventBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(RecordWithdrawal),
            step: async () =>
            {
                var builder = new MoneyWithdrawnIntegrationEventBuilder();

                configure?.Invoke(builder);

                if (builder.AccountId is null) {
                    builder.Of(await LastOrNewAccountIdAsync());
                }

                await GetService<MoneyWithdrawnIntegrationEventHandler>().HandleAsync(
                    integrationEvent: builder.Build(),
                    cancellationToken: CancellationToken.None
                );
            }
        );

        return this;
    }
}
