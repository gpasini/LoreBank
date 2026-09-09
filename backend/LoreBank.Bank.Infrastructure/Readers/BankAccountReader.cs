using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Application.Queries.ListBankAccounts;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Infrastructure.Readers;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Readers;

// Lit la row, pas l'agrégat : aucun value object n'est reconstruit, aucune
// entité n'est suivie, et la projection du Select final ne fait lire à EF que
// les colonnes du Result. Le lien colonne → propriété vit dans la
// configuration de BankAccountRow, en chaînes que rien ne compile :
// GetBankAccountByIdTest et ListBankAccountsTest l'épinglent en relisant
// chaque champ après écriture.
public sealed class BankAccountReader(BankDbContext context) : ModuleReader(context), IBankAccountReader
{
    public Task<BankAccountResult?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => Query<BankAccountRow>()
        .Where(row => row.Id == id)
        .Select(row => new BankAccountResult(
            row.Id,
            row.Iban,
            row.BalanceAmount,
            row.BalanceCurrency,
            row.IsClosed,
            row.OpenedBy,
            row.OpenedAt
        ))
        .SingleOrDefaultAsync(cancellationToken);

    // La Liste (ADR 0027) se déclare, le moteur du socle l'exécute : recherche
    // sur l'IBAN, devise et clôture filtrées et facettées sous le nom de la
    // propriété de la query, tri par IBAN.
    public Task<ListPage<BankAccountSummaryResult>> ListAsync(
        ListBankAccountsQuery query,
        CancellationToken cancellationToken
    ) => Query<BankAccountRow>()
        .List(query)
        .SearchIn(row => row.Iban)
        .Filter(
            values: query.Currency,
            column: row => row.BalanceCurrency,
            facet: nameof(query.Currency)
        )
        .Filter(
            values: query.IsClosed,
            column: row => row.IsClosed,
            facet: nameof(query.IsClosed)
        )
        .OrderBy(row => row.Iban)
        .ToPageAsync(
            projection: row => new BankAccountSummaryResult(
                row.Id,
                row.Iban,
                row.BalanceAmount,
                row.BalanceCurrency,
                row.IsClosed,
                row.OpenedAt
            ),
            cancellationToken: cancellationToken
        );
}
