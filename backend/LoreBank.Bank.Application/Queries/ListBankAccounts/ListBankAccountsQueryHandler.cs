using LoreBank.Bank.Application.Readers;
using LoreBank.SharedKernel.Application;
using MediatR;

namespace LoreBank.Bank.Application.Queries.ListBankAccounts;

// Une Page vide n'est pas une absence : la Page est toujours servie, il n'y a
// pas de 404 à lever ici.
public sealed class ListBankAccountsQueryHandler(IBankAccountReader reader)
    : IRequestHandler<ListBankAccountsQuery, ListPage<BankAccountSummaryResult>>
{
    public Task<ListPage<BankAccountSummaryResult>> Handle(
        ListBankAccountsQuery request,
        CancellationToken cancellationToken
    ) => reader.ListAsync(
        query: request,
        cancellationToken: cancellationToken
    );
}
