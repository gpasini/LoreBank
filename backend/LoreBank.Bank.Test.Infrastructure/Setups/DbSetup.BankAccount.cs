using LoreBank.Bank.Application.Commands.DepositMoney;
using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Domain.Aggregates;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public partial class DbSetup
{
    private readonly List<BankAccountId> _bankAccountIds = [];

    public async Task CreateBankAccountAsync(
        string iban = "FR7630006000011234567890189",
        string currency = "EUR",
        decimal balance = 0m
    )
    {
        var accountId = await Sender.Send(
            new OpenBankAccountCommand(
                Iban: iban,
                Currency: currency
            )
        );

        if (balance > 0m) {
            await Sender.Send(
                new DepositMoneyCommand(
                    AccountId: accountId,
                    Amount: balance,
                    Currency: currency
                )
            );
        }

        _bankAccountIds.Add(BankAccountId.Hydrate(accountId));
    }

    public BankAccountId GetLastBankAccountId() => _bankAccountIds.Last();
}
