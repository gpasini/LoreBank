using LoreBank.Bank.Contracts.Readers;
using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Application.Readers;
using MediatR;

namespace LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

public sealed class GetBankAccountLedgerQueryHandler(
    IBankAccountsContract bankAccounts,
    ILedgerMovementReader reader
) : IRequestHandler<GetBankAccountLedgerQuery, BankAccountLedgerResult>
{
    // Le port publié de Bank rend null pour un compte inconnu — pour lui,
    // l'absence est un résultat normal ; c'est ce handler qui la transforme
    // en erreur métier du Ledger, avec le code du module.
    public async Task<BankAccountLedgerResult> Handle(
        GetBankAccountLedgerQuery request,
        CancellationToken cancellationToken
    )
    {
        var account = await bankAccounts.FindByIdAsync(
            accountId: request.AccountId,
            cancellationToken: cancellationToken
        ) ?? throw new UnknownBankAccountException(request.AccountId);

        var movements = await reader.GetByBankAccountAsync(
            bankAccountId: request.AccountId,
            cancellationToken: cancellationToken
        );

        return new BankAccountLedgerResult(
            AccountId: account.Id,
            Iban: account.Iban,
            Movements: movements
        );
    }
}
