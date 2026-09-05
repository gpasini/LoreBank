using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.EventHandlers;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Test.Unit.Fakes;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.DomainEvents;

[TestFixture]
[TestOf(typeof(MoneyWithdrawnDomainEventHandler))]
public sealed class MoneyWithdrawnDomainEventHandlerTest
{
    [Test]
    public async Task HandleAsync_ShouldPublishTheFlattenedTwin()
    {
        // Arrange

        var publisher = new FakeIntegrationEventPublisher();
        var handler = new MoneyWithdrawnDomainEventHandler(publisher);
        var accountId = BankAccountId.New();

        var domainEvent = new MoneyWithdrawnDomainEvent(
            AccountId: accountId,
            Amount: Money.Of(
                amount: 10m,
                currency: "EUR"
            ),
            NewBalance: Money.Of(
                amount: 60m,
                currency: "EUR"
            )
        );

        // Act

        await handler.HandleAsync(
            domainEvent: domainEvent,
            cancellationToken: CancellationToken.None
        );

        // Assert

        publisher.Published.Should().ContainSingle().Which.Should().Be(
            new MoneyWithdrawnIntegrationEvent(
                AccountId: accountId.Value,
                Amount: 10m,
                Currency: "EUR"
            )
        );
    }
}
