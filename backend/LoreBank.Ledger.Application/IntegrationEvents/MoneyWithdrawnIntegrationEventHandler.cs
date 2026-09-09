using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Repositories;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Application.IntegrationEvents;

// Le miroir du dépôt : un retrait fait sortir de l'argent — débit compte
// client, crédit trésorerie.
public sealed class MoneyWithdrawnIntegrationEventHandler(
    IJournalEntryRepository repository,
    TimeProvider timeProvider
) : IIntegrationEventHandler<MoneyWithdrawnIntegrationEvent>
{
    public Task HandleAsync(
        MoneyWithdrawnIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        var amount = PositiveMoney.Of(
            amount: integrationEvent.Amount,
            currency: integrationEvent.Currency
        );

        return repository.SaveAsync(
            entry: JournalEntry.Record(
                lines: [
                JournalLine.Of(
                    account: LedgerAccountRef.ForBankAccount(integrationEvent.AccountId),
                    direction: JournalDirection.Debit,
                    amount: amount
                ),
                JournalLine.Of(
                    account: LedgerAccountRef.Cash,
                    direction: JournalDirection.Credit,
                    amount: amount
                ),
                ],
                recordedAt: timeProvider.GetUtcNow()
            ),
            cancellationToken: cancellationToken
        );
    }
}
