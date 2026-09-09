using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Repositories;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Application.IntegrationEvents;

// La réaction du Ledger au fait publié par Bank : un dépôt fait entrer de
// l'argent — débit trésorerie, crédit compte client. Le handler tourne dans
// sa propre transaction, livré at-least-once par le socle : l'inbox le
// protège du rejeu, il n'a que son métier à écrire (ADR 0014). L'Instant de
// comptabilisation vient de TimeProvider (ADR 0024) : celui de
// l'enregistrement par le Ledger, pas celui du dépôt chez Bank.
public sealed class MoneyDepositedIntegrationEventHandler(
    IJournalEntryRepository repository,
    TimeProvider timeProvider
) : IIntegrationEventHandler<MoneyDepositedIntegrationEvent>
{
    public Task HandleAsync(
        MoneyDepositedIntegrationEvent integrationEvent,
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
                    account: LedgerAccountRef.Cash,
                    direction: JournalDirection.Debit,
                    amount: amount
                ),
                JournalLine.Of(
                    account: LedgerAccountRef.ForBankAccount(integrationEvent.AccountId),
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
