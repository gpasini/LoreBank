using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Commands.WithdrawMoney;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Currency
) : ICommand;
