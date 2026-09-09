using LoreBank.Ledger.Application.Queries.ListLedgerMovements;
using LoreBank.Ledger.Application.Readers;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.Ledger.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.Ledger.Infrastructure.Readers;

// Lit les rows des jambes et des écritures, pas l'agrégat : la jointure est
// composée en LINQ, puis la Liste se déclare au moteur du socle (ADR 0027)
// sur le résultat joint — recherche sur l'identifiant d'écriture, sens
// filtré et facetté, du plus récent au plus ancien (l'identifiant
// d'écriture départageant deux Instants égaux). La projection finale ne
// fait lire que les colonnes de l'item. Le lien colonne → propriété vit
// dans les configurations des rows, en chaînes que rien ne compile :
// ListLedgerMovementsTest relit chaque champ.
public sealed class LedgerMovementReader(LedgerDbContext context)
    : ModuleReader(context), ILedgerMovementReader
{
    public Task<ListPage<LedgerMovementResult>> ListAsync(
        ListLedgerMovementsQuery query,
        CancellationToken cancellationToken
    )
    {
        // Le VO fabrique la forme canonique de la référence : la requête ne la
        // connaît pas, une évolution du format ne se corrige qu'au VO.
        var accountRef = LedgerAccountRef.ForBankAccount(query.AccountId).Value;

        return Query<JournalLineRow>()
            .Where(line => line.AccountRef == accountRef)
            .Join(
                inner: Query<JournalEntryRow>(),
                outerKeySelector: line => line.JournalEntryId,
                innerKeySelector: entry => entry.Id,
                resultSelector: (
                    line,
                    entry
                ) => new {
                    Line = line,
                    Entry = entry,
                }
            )
            .List(query)
            .SearchIn(movement => movement.Line.JournalEntryId.ToString())
            .Filter(
                values: query.Direction,
                column: movement => movement.Line.Direction,
                facet: nameof(query.Direction)
            )
            .OrderByDescending(movement => movement.Entry.RecordedAt)
            .ThenByDescending(movement => movement.Entry.Id)
            .ToPageAsync(
                projection: movement => new LedgerMovementResult(
                    movement.Line.JournalEntryId,
                    movement.Line.Direction,
                    movement.Line.Amount,
                    movement.Line.Currency,
                    movement.Entry.RecordedAt
                ),
                cancellationToken: cancellationToken
            );
    }
}
