using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Application.Queries.ListBankAccounts;
using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Readers;

// Port de lecture, distinct du repository de l'agrégat. Un repository charge un
// agrégat pour le muter : il reconstruit ses value objects et le fait suivre au
// change tracker. Une lecture n'a besoin de rien de tout ça — seulement des
// colonnes qu'elle affiche.
//
// Le port rend `null` quand la ligne n'existe pas : l'absence est un résultat
// normal pour un lecteur. C'est l'Application qui décide qu'elle est une erreur.
public interface IBankAccountReader
{
    Task<BankAccountResult?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    );

    // La Liste des comptes (ADR 0027), triée par IBAN : le reader reçoit la
    // query entière — c'est elle qui porte page, recherche et filtres — et
    // rend une Page, jamais null : une liste n'a pas d'absence.
    Task<ListPage<BankAccountSummaryResult>> ListAsync(
        ListBankAccountsQuery query,
        CancellationToken cancellationToken
    );
}
