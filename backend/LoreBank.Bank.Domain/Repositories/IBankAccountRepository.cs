using LoreBank.Bank.Domain.Aggregates;

namespace LoreBank.Bank.Domain.Repositories;

public interface IBankAccountRepository
{
    Task<BankAccount?> GetByIdAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    );

    Task SaveAsync(
        BankAccount account,
        CancellationToken cancellationToken
    );
}
