using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace LoreBank.Bank.Application.Commands.OpenBankAccount;

// L'Acteur vient du port, pas de la commande (ADR 0023) : c'est un contexte
// de la requête, jamais un champ que le client poste.
public sealed class OpenBankAccountCommandHandler(
    IBankAccountRepository repository,
    ICurrentActor currentActor
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
            openedBy: currentActor.Actor
        );

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );

        return account.Id.Value;
    }
}
