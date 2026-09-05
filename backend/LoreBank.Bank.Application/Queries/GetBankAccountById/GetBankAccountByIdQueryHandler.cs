using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Domain.Aggregates;
using MediatR;

namespace LoreBank.Bank.Application.Queries.GetBankAccountById;

public sealed class GetBankAccountByIdQueryHandler(IBankAccountReader reader)
    : IRequestHandler<GetBankAccountByIdQuery, BankAccountResult>
{
    // La lecture ne passe pas par l'agrégat : elle interroge un port de lecture,
    // que l'Infrastructure sert en tapant la table directement.
    //
    // L'absence est une erreur, pas une valeur de retour : c'est ce qui fait que
    // tout 404 de l'API porte un code, qu'on soit passé par une commande ou par
    // une lecture. Le controller n'a donc aucun cas d'absence à traiter.
    public async Task<BankAccountResult> Handle(
        GetBankAccountByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var result = await reader.GetByIdAsync(
            id: request.AccountId,
            cancellationToken: cancellationToken
        );

        return result ?? throw new BankAccountNotFoundException(BankAccountId.Hydrate(request.AccountId));
    }
}
