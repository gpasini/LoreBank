using System.Data.Common;
using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;
using LoreBank.Ledger.Application.Readers;
using LoreBank.Ledger.Domain.ValueObjects;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.Ledger.Infrastructure.Readers;

// Lit la table des jambes, pas l'agrégat : le SELECT ne ramène que les
// colonnes du Result. Le lien colonne → propriété n'est vérifié par aucun
// compilateur : GetBankAccountLedgerTest relit chaque champ.
public sealed class LedgerMovementReader(LedgerDbContext context)
    : ModuleReader(context), ILedgerMovementReader
{
    private string SelectByAccountRef =>
        $"""
        SELECT journal_entry_id, direction, amount, currency
        FROM {Schema}.journal_lines
        WHERE account_ref = @accountRef
        ORDER BY journal_entry_id
        """;

    public Task<IReadOnlyList<LedgerMovementResult>> GetByBankAccountAsync(
        Guid bankAccountId,
        CancellationToken cancellationToken
    ) => QueryAsync(
        sql: SelectByAccountRef,
        // Le VO fabrique la forme canonique de la référence : le SQL ne la
        // connaît pas, une évolution du format ne se corrige qu'au VO.
        parameters: new() { ["accountRef"] = LedgerAccountRef.ForBankAccount(bankAccountId).Value },
        map: Map,
        cancellationToken: cancellationToken
    );

    private static LedgerMovementResult Map(DbDataReader reader) => new(
        EntryId: reader.GetGuid(0),
        Direction: reader.GetString(1),
        Amount: reader.GetDecimal(2),
        Currency: reader.GetString(3)
    );
}
