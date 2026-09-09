using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;
using LoreBank.Ledger.Application.Readers;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.Ledger.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Infrastructure.Readers;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Ledger.Infrastructure.Readers;

// Lit les rows des jambes et des écritures, pas l'agrégat : la jointure est
// composée en LINQ, la projection du Select final ne fait lire que les
// colonnes du Result. Les mouvements sortent dans l'ordre de comptabilisation
// (ADR 0024), l'identifiant d'écriture départageant deux Instants égaux. Le
// lien colonne → propriété vit dans les configurations des rows, en chaînes
// que rien ne compile : GetBankAccountLedgerTest relit chaque champ.
public sealed class LedgerMovementReader(LedgerDbContext context)
    : ModuleReader(context), ILedgerMovementReader
{
    public async Task<IReadOnlyList<LedgerMovementResult>> GetByBankAccountAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken
    )
    {
        // Le VO fabrique la forme canonique de la référence : la requête ne la
        // connaît pas, une évolution du format ne se corrige qu'au VO.
        var accountRef = LedgerAccountRef.ForBankAccount(bankAccountId).Value;

        return await Query<JournalLineRow>()
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
            .OrderBy(movement => movement.Entry.RecordedAt)
            .ThenBy(movement => movement.Entry.Id)
            .Select(movement => new LedgerMovementResult(
                movement.Line.JournalEntryId,
                movement.Line.Direction,
                movement.Line.Amount,
                movement.Line.Currency,
                movement.Entry.RecordedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
