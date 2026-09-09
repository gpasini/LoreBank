using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace LoreBank.Bank.Application.Commands.OpenBankAccount;

// L'Acteur vient du port, pas de la commande (ADR 0023), et l'Instant de
// TimeProvider (ADR 0024) : des contextes de la requête, jamais des champs que
// le client poste — et le Domain les reçoit, il ne les demande pas.
public sealed class OpenBankAccountCommandHandler(
    IBankAccountRepository repository,
    ICurrentActor currentActor,
    TimeProvider timeProvider
) : IRequestHandler<OpenBankAccountCommand, Guid>
{
    public async Task<Guid> Handle(
        OpenBankAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        var account = BankAccount.Open(
            iban: Iban.Parse(request.Iban),
            currency: request.Currency,
            openedBy: currentActor.Actor,
            openedAt: timeProvider.GetUtcNow()
        );

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );

        return account.Id.Value;
    }
}
