using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace LoreBank.Bank.Application.Commands.OpenBankAccount;

public sealed class OpenBankAccountCommandHandler(IBankAccountRepository repository)
    : IRequestHandler<OpenBankAccountCommand, Guid>
{
    public async Task<Guid> Handle(
        OpenBankAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        var account = BankAccount.Open(
            iban: new Iban(request.Iban),
            currency: request.Currency
        );

        await repository.SaveAsync(
            account: account,
            cancellationToken: cancellationToken
        );

        return account.Id.Value;
    }
}
