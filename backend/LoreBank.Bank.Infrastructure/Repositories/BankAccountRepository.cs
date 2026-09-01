using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.Bank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Repositories;

public sealed class BankAccountRepository(BankDbContext context) : IBankAccountRepository
{
    public async Task<BankAccount?> GetByIdAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    ) => await context.BankAccounts.SingleOrDefaultAsync(
        predicate: account => account.Id == id,
        cancellationToken: cancellationToken
    );

    public async Task SaveAsync(
        BankAccount account,
        CancellationToken cancellationToken
    )
    {
        if (context.Entry(account).State == EntityState.Detached) {
            context.BankAccounts.Add(account);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
