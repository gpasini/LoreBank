using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace LoreBank.Bank.Application.Commands;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Currency
) : ICommand;

public sealed class WithdrawMoneyCommandHandler(IBankAccountRepository repository)
    : IRequestHandler<WithdrawMoneyCommand>
{
    public async Task Handle(
        WithdrawMoneyCommand request,
        CancellationToken cancellationToken
    )
    {
        var accountId = new BankAccountId(request.AccountId);

        var account = await repository.GetRequiredByIdAsync(
            id: accountId,
            cancellationToken: cancellationToken
        );

        account.Withdraw(
            new PositiveMoney(
                amount: request.Amount,
                currency: request.Currency
            )
        );

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );
    }
}
