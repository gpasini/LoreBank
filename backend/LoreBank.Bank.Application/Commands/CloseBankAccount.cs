using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.SharedKernel.Application;
using MediatR;

namespace LoreBank.Bank.Application.Commands;

public sealed record CloseBankAccountCommand(Guid AccountId) : ICommand;

public sealed class CloseBankAccountCommandHandler(IBankAccountRepository repository)
    : IRequestHandler<CloseBankAccountCommand>
{
    public async Task Handle(
        CloseBankAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        var accountId = new BankAccountId(request.AccountId);

        var account = await repository.GetByIdAsync(
            id: accountId,
            cancellationToken: cancellationToken
        ) ?? throw new BankAccountNotFoundException(accountId);

        account.Close();

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );
    }
}
