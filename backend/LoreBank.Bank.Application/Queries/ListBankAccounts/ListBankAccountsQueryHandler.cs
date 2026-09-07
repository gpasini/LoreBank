using LoreBank.Bank.Application.Readers;
using MediatR;

namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

// Une liste vide n'est pas une absence : le Result est toujours servi, il
// n'y a pas de 404 à lever ici.
public sealed class ListBankAccountsQueryHandler(IBankAccountReader reader)
    : IRequestHandler<ListBankAccountsQuery, BankAccountsResult>
{
    public async Task<BankAccountsResult> Handle(
        ListBankAccountsQuery request,
        CancellationToken cancellationToken
    ) => new(await reader.ListAsync(cancellationToken));
}
