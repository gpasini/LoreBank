using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Domain.Aggregates;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public partial class DbSetup
{
    private readonly List<BankAccountId> _bankAccountIds = [];

    public DbSetup CreateBankAccount(
        string iban = "FR7630006000011234567890189",
        string currency = "EUR",
        decimal balance = 0m
    )
    {
        var sender = serviceProvider.GetRequiredService<ISender>();

        var accountId = sender
            .Send(
                new OpenBankAccountCommand(
                    Iban: iban,
                    Currency: currency
                )
            )
            .Result;

        if (balance > 0m) {
            sender
                .Send(
                    new DepositMoneyCommand(
                        AccountId: accountId,
                        Amount: balance,
                        Currency: currency
                    )
                )
                .Wait();
        }

        _bankAccountIds.Add(new BankAccountId(accountId));

        return this;
    }

    public BankAccountId GetLastBankAccountId() => _bankAccountIds.Last();
}
