using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Services;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Domain.EventHandlers;

public sealed class BankAccountOpenedDomainEventHandler(IWelcomeLetterSender welcomeLetterSender)
    : IDomainEventHandler<BankAccountOpened>
{
    public Task HandleAsync(
        BankAccountOpened domainEvent,
        CancellationToken cancellationToken
    ) => welcomeLetterSender.SendAsync(
        iban: domainEvent.Iban,
        cancellationToken: cancellationToken
    );
}
