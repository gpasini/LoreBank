using LoreBank.Bank.Domain.Aggregates;

namespace LoreBank.Bank.Domain.Repositories;

public interface IBankAccountRepository
{
    // Non nullable : l'absence est une erreur métier, le repository lève la
    // NotFoundException du module. Pas de variante nullable — une commande de
    // création ne charge pas, et une sonde d'existence n'est pas un usage.
    Task<BankAccount> GetRequiredByIdAsync(
        BankAccountId id,
        CancellationToken cancellationToken
    );

    Task SaveAsync(
        BankAccount account,
        CancellationToken cancellationToken
    );
}
