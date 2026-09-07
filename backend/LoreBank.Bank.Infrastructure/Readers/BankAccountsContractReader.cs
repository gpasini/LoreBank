using LoreBank.Bank.Contracts.Readers;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Infrastructure.Readers;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Readers;

// L'implémentation du port de lecture publié : même discipline que
// BankAccountReader — la row partagée du module, et une projection qui ne
// fait lire que les colonnes du DTO. BankAccountsContractReaderTest relit
// chaque champ après écriture.
public sealed class BankAccountsContractReader(BankDbContext context)
    : ModuleReader(context), IBankAccountsContract
{
    public Task<BankAccountSummary?> FindByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken
    ) => Query<BankAccountRow>()
        .Where(row => row.Id == accountId)
        .Select(row => new BankAccountSummary(
            row.Id,
            row.Iban
        ))
        .SingleOrDefaultAsync(cancellationToken);
}
