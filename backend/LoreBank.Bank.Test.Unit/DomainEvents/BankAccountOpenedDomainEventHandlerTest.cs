using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.EventHandlers;
using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Test.Unit.Fakes;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.DomainEvents;

[TestFixture]
[TestOf(typeof(BankAccountOpenedDomainEventHandler))]
public sealed class BankAccountOpenedDomainEventHandlerTest
{
    [Test]
    public async Task HandleAsync_ShouldSendWelcomeLetterToAccountIban()
    {
        // Arrange

        var welcomeLetterSender = new FakeWelcomeLetterSender();
        var handler = new BankAccountOpenedDomainEventHandler(welcomeLetterSender);
        var iban = new Iban("FR7630006000011234567890189");

        var domainEvent = new BankAccountOpened(
            AccountId: BankAccountId.New(),
            Iban: iban
        );

        // Act

        await handler.HandleAsync(
            domainEvent: domainEvent,
            cancellationToken: CancellationToken.None
        );

        // Assert

        welcomeLetterSender.SentTo.Should().Be(iban);
    }
}
