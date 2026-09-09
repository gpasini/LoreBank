using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Application.IntegrationEvents;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Test.Unit.Fakes;

namespace LoreBank.Ledger.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(MoneyWithdrawnIntegrationEventHandler))]
public sealed class MoneyWithdrawnIntegrationEventHandlerTest
{
    [Test]
    public async Task HandleAsync_ShouldRecordDebitCustomerCreditCash()
    {
        // Arrange

        var repository = new FakeJournalEntryRepository();
        var handler = new MoneyWithdrawnIntegrationEventHandler(
            repository: repository,
            timeProvider: new FixedTimeProvider(DateTimeOffset.UnixEpoch)
        );
        var accountId = Guid.NewGuid();

        // Act

        await handler.HandleAsync(
            integrationEvent: new MoneyWithdrawnIntegrationEvent(
                AccountId: accountId,
                Amount: 10m,
                Currency: "EUR"
            ),
            cancellationToken: CancellationToken.None
        );

        // Assert

        var entry = repository.Saved.Should().ContainSingle().Subject;

        var debit = entry.Lines.Single(line => line.Direction == JournalDirection.Debit);
        var credit = entry.Lines.Single(line => line.Direction == JournalDirection.Credit);

        debit.Account.Should().Be(LedgerAccountRef.ForBankAccount(accountId));
        credit.Account.Should().Be(LedgerAccountRef.Cash);
        debit.Amount.Amount.Should().Be(10m);
    }
}
