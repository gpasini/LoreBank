using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using MediatR;

namespace LoreBank.Bank.Application.Commands.CloseBankAccount;

public sealed class CloseBankAccountCommandHandler(IBankAccountRepository repository)
    : IRequestHandler<CloseBankAccountCommand>
{
    public async Task Handle(
        CloseBankAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        var accountId = new BankAccountId(request.AccountId);

        var account = await repository.GetRequiredByIdAsync(
            id: accountId,
            cancellationToken: cancellationToken
        );

        account.Close();

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );
    }
}
