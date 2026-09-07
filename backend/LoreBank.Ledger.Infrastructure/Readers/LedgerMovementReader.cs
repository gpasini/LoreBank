using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;
using LoreBank.Ledger.Application.Readers;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.Ledger.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Infrastructure.Readers;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Ledger.Infrastructure.Readers;

// Lit la row des jambes, pas l'agrégat : la projection du Select final ne
// fait lire que les colonnes du Result. Le lien colonne → propriété vit dans
// la configuration de JournalLineRow, en chaînes que rien ne compile :
// GetBankAccountLedgerTest relit chaque champ.
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
            .Where(row => row.AccountRef == accountRef)
            .OrderBy(row => row.JournalEntryId)
            .Select(row => new LedgerMovementResult(
                row.JournalEntryId,
                row.Direction,
                row.Amount,
                row.Currency
            ))
            .ToListAsync(cancellationToken);
    }
}
