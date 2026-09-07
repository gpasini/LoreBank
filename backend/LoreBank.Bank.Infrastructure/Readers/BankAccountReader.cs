using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Application.Queries.ListBankAccounts;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.ReadRows;
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
            row.IsClosed
        ))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BankAccountSummaryResult>> ListAsync(CancellationToken cancellationToken) =>
        await Query<BankAccountRow>()
            .OrderBy(row => row.Iban)
            .Select(row => new BankAccountSummaryResult(
                row.Id,
                row.Iban,
                row.BalanceAmount,
                row.BalanceCurrency,
                row.IsClosed
            ))
            .ToListAsync(cancellationToken);
}
