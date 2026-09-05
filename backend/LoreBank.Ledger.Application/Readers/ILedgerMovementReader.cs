using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

namespace LoreBank.Ledger.Application.Readers;

// Port de lecture du module : les jambes d'écriture qui touchent un compte
// client. Une liste vide est un résultat normal — un compte sans mouvement
// a un ledger vide, pas une erreur.
public interface ILedgerMovementReader
{
    Task<IReadOnlyList<LedgerMovementResult>> GetByBankAccountAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken
    );
}
