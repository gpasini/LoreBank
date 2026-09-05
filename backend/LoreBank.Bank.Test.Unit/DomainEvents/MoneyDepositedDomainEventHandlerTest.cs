using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.EventHandlers;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Test.Unit.Fakes;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.DomainEvents;

[TestFixture]
[TestOf(typeof(MoneyDepositedDomainEventHandler))]
public sealed class MoneyDepositedDomainEventHandlerTest
{
    [Test]
    public async Task HandleAsync_ShouldPublishTheFlattenedTwin()
    {
        // Arrange

        var publisher = new FakeIntegrationEventPublisher();
        var handler = new MoneyDepositedDomainEventHandler(publisher);
        var accountId = BankAccountId.New();

        var domainEvent = new MoneyDepositedDomainEvent(
            AccountId: accountId,
            Amount: Money.Of(
                amount: 25.50m,
                currency: "EUR"
            ),
            NewBalance: Money.Of(
                amount: 70m,
                currency: "EUR"
            )
        );

        // Act

        await handler.HandleAsync(
            domainEvent: domainEvent,
            cancellationToken: CancellationToken.None
        );

        // Assert

        // Des primitives, jamais des VO — et pas le solde : le contrat ne
        // porte que ce que ses consommateurs consomment.
        publisher.Published.Should().ContainSingle().Which.Should().Be(
            new MoneyDepositedIntegrationEvent(
                AccountId: accountId.Value,
                Amount: 25.50m,
                Currency: "EUR"
            )
        );
    }
}
