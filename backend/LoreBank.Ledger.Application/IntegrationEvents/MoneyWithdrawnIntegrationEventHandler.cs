using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Repositories;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Application.IntegrationEvents;

// Le miroir du dépôt : un retrait fait sortir de l'argent — débit compte
// client, crédit trésorerie.
public sealed class MoneyWithdrawnIntegrationEventHandler(IJournalEntryRepository repository)
    : IIntegrationEventHandler<MoneyWithdrawnIntegrationEvent>
{
    public Task HandleAsync(
        MoneyWithdrawnIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    )
    {
        var amount = new PositiveMoney(
            amount: integrationEvent.Amount,
            currency: integrationEvent.Currency
        );

        return repository.SaveAsync(
            entry: JournalEntry.Record([
                new JournalLine(
                    account: LedgerAccountRef.ForBankAccount(integrationEvent.AccountId),
                    direction: JournalDirection.Debit,
                    amount: amount
                ),
                new JournalLine(
                    account: LedgerAccountRef.Cash,
                    direction: JournalDirection.Credit,
                    amount: amount
                ),
            ]),
            cancellationToken: cancellationToken
        );
    }
}
