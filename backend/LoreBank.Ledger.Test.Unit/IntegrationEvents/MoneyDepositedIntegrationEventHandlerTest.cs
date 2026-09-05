using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Application.IntegrationEvents;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Test.Unit.Fakes;

namespace LoreBank.Ledger.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(MoneyDepositedIntegrationEventHandler))]
public sealed class MoneyDepositedIntegrationEventHandlerTest
{
    [Test]
    public async Task HandleAsync_ShouldRecordDebitCashCreditCustomer()
    {
        // Arrange

        var repository = new FakeJournalEntryRepository();
        var handler = new MoneyDepositedIntegrationEventHandler(repository);
        var accountId = Guid.NewGuid();

        // Act

        await handler.HandleAsync(
            integrationEvent: new MoneyDepositedIntegrationEvent(
                AccountId: accountId,
                Amount: 25.50m,
                Currency: "EUR"
            ),
            cancellationToken: CancellationToken.None
        );

        // Assert

        var entry = repository.Saved.Should().ContainSingle().Subject;

        entry.Lines.Should().HaveCount(2);

        var debit = entry.Lines.Single(line => line.Direction == JournalDirection.Debit);
        var credit = entry.Lines.Single(line => line.Direction == JournalDirection.Credit);

        debit.Account.Should().Be(LedgerAccountRef.Cash);
        credit.Account.Should().Be(LedgerAccountRef.ForBankAccount(accountId));
        debit.Amount.Amount.Should().Be(25.50m);
        credit.Amount.Amount.Should().Be(25.50m);
        debit.Amount.Currency.Should().Be("EUR");
    }
}
