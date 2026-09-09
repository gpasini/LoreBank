using LoreBank.Ledger.Application.Queries.ListLedgerMovements;
using LoreBank.SharedKernel.Application;

namespace LoreBank.Ledger.Application.Readers;

// Port de lecture du module : les jambes d'écriture qui touchent un compte
// client, en Liste (ADR 0027) — le reader reçoit la query entière, compte,
// page, recherche et filtre compris. Une Page vide est un résultat normal :
// un compte sans mouvement a un ledger vide, pas une erreur.
public interface ILedgerMovementReader
{
    Task<ListPage<LedgerMovementResult>> ListAsync(
        ListLedgerMovementsQuery query,
        CancellationToken cancellationToken
    );
}
