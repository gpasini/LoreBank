using LoreBank.Bank.Contracts.Readers;
using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Application.Readers;
using LoreBank.SharedKernel.Application;
using MediatR;

namespace LoreBank.Ledger.Application.Queries.ListLedgerMovements;

// Le point de rencontre des deux canaux de communication : le port publié de
// Bank dit si le compte existe — il rend null pour un compte inconnu, pour
// lui l'absence est un résultat normal ; c'est ce handler qui la transforme
// en erreur métier du Ledger, avec le code du module — et le reader du
// module sert la Page. Une Page vide n'est pas une absence : un compte sans
// mouvement a un ledger vide, seule l'absence du compte est une erreur.
public sealed class ListLedgerMovementsQueryHandler(
    IBankAccountsContract bankAccounts,
    ILedgerMovementReader reader
) : IRequestHandler<ListLedgerMovementsQuery, ListPage<LedgerMovementResult>>
{
    public async Task<ListPage<LedgerMovementResult>> Handle(
        ListLedgerMovementsQuery request,
        CancellationToken cancellationToken
    )
    {
        _ = await bankAccounts.FindByIdAsync(
            accountId: request.AccountId,
            cancellationToken: cancellationToken
        ) ?? throw new UnknownBankAccountException(request.AccountId);

        return await reader.ListAsync(
            query: request,
            cancellationToken: cancellationToken
        );
    }
}
