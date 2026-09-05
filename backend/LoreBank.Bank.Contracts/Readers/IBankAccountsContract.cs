namespace LoreBank.Bank.Contracts.Readers;

// Le port de lecture publié du module : la façon dont un autre module lit
// Bank en synchrone, sans traverser la frontière — DTOs plats, jamais les
// agrégats, implémenté chez Bank comme un reader SQL. In-process et lecture
// pure : pas d'escalade de transaction. Comme tout lecteur, il rend null
// quand la ligne n'existe pas — l'absence est un résultat normal, le
// consommateur décide de ce qu'elle signifie chez lui.
public interface IBankAccountsContract
{
    Task<BankAccountSummary?> FindByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken
    );
}
